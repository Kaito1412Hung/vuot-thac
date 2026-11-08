using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.Backend
{
    /// <summary>
    /// Đường đi của một lượt đua, 10 mẫu mỗi giây. Định dạng khớp với Backend/README.md:
    /// "VTG1", uint16 số mẫu mỗi giây, uint32 số khung, rồi mỗi khung 3 float32 vị trí và int16 hướng (phần trăm độ).
    /// </summary>
    public sealed class GhostData
    {
        public const int Hz = 10;
        const int MaxBytes = 1024 * 1024;
        static readonly byte[] Magic = { (byte)'V', (byte)'T', (byte)'G', (byte)'1' };

        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<float> Headings = new List<float>();

        public void Sample(float seconds, out Vector3 position, out Quaternion rotation)
        {
            float frame = Mathf.Max(0, seconds) * Hz;
            int a = Mathf.Min(Mathf.FloorToInt(frame), Positions.Count - 1), b = Mathf.Min(a + 1, Positions.Count - 1);
            float t = Mathf.Clamp01(frame - a);
            position = Vector3.Lerp(Positions[a], Positions[b], t);
            rotation = Quaternion.Euler(0, Mathf.LerpAngle(Headings[a], Headings[b], t), 0);
        }

        public byte[] ToGzip()
        {
            using var memory = new MemoryStream();
            using (var gzip = new GZipStream(memory, System.IO.Compression.CompressionLevel.Optimal, true))
            using (var writer = new BinaryWriter(gzip))
            {
                writer.Write(Magic);
                writer.Write((ushort)Hz);
                writer.Write((uint)Positions.Count);
                for (int i = 0; i < Positions.Count; i++)
                {
                    writer.Write(Positions[i].x); writer.Write(Positions[i].y); writer.Write(Positions[i].z);
                    writer.Write((short)Mathf.RoundToInt(Mathf.DeltaAngle(0, Headings[i]) * 100));
                }
            }
            return memory.ToArray();
        }

        /// <summary>Đọc file tải về. Dữ liệu sai định dạng thì trả về null, không làm hỏng game.</summary>
        public static GhostData FromGzip(byte[] data)
        {
            try
            {
                using var input = new MemoryStream(data);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    raw.Write(buffer, 0, read);
                    if (raw.Length > MaxBytes) return null;
                }
                raw.Position = 0;
                using var reader = new BinaryReader(raw);
                var magic = reader.ReadBytes(4);
                for (int i = 0; i < 4; i++) if (magic[i] != Magic[i]) return null;
                if (reader.ReadUInt16() != Hz) return null;
                uint count = reader.ReadUInt32();
                if (count == 0 || raw.Length != 10 + count * 14L) return null;
                var ghost = new GhostData();
                for (uint i = 0; i < count; i++)
                {
                    ghost.Positions.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    ghost.Headings.Add(reader.ReadInt16() / 100f);
                }
                return ghost;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is IndexOutOfRangeException)
            {
                Debug.LogWarning("[Backend] Ghost hỏng: " + error.Message);
                return null;
            }
        }
    }

    /// <summary>Ghi vị trí thuyền theo đồng hồ đua. Khung hình chậm thì nội suy, để số mẫu luôn khớp thời gian.</summary>
    public sealed class GhostRecorder
    {
        public readonly GhostData Data = new GhostData();
        float nextSample, lastTime, lastHeading;
        Vector3 lastPosition;

        public void Tick(float elapsed, Transform boat)
        {
            float heading = boat.eulerAngles.y;
            var position = boat.position;
            if (Data.Positions.Count == 0) { lastPosition = position; lastHeading = heading; lastTime = 0; }
            while (nextSample <= elapsed)
            {
                float t = elapsed > lastTime ? Mathf.Clamp01((nextSample - lastTime) / (elapsed - lastTime)) : 1;
                Data.Positions.Add(Vector3.Lerp(lastPosition, position, t));
                Data.Headings.Add(Mathf.LerpAngle(lastHeading, heading, t));
                nextSample += 1f / GhostData.Hz;
            }
            lastPosition = position; lastHeading = heading; lastTime = elapsed;
        }
    }

    /// <summary>Thuyền bóng ma: đi theo đường đã ghi, không va chạm, không ảnh hưởng cuộc đua.</summary>
    public sealed class GhostBoat : MonoBehaviour
    {
        GhostData data;
        BoatRace race;

        public static GameObject Spawn(GameObject prefab, GhostData data, BoatRace race, Material material)
        {
            var go = Instantiate(prefab);
            go.name = "GhostBoat";
            var rigidbody = go.GetComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            // Bóng ma không làm nổi sóng và không khoét mặt nước
            foreach (var fx in go.GetComponents<BoatWaterEffects>()) Destroy(fx);
            foreach (var clip in go.GetComponentsInChildren<BoatInteriorWaterClip>()) Destroy(clip);
            foreach (var particles in go.GetComponentsInChildren<ParticleSystem>()) Destroy(particles.gameObject);
            var foam = go.transform.Find("ContactFoam");
            if (foam != null) Destroy(foam.gameObject);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "DryInteriorWaterMask") { renderer.enabled = false; continue; }
                var materials = new Material[renderer.sharedMaterials.Length];
                Array.Fill(materials, material);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var ghost = go.AddComponent<GhostBoat>();
            ghost.data = data; ghost.race = race;
            ghost.Follow();
            return go;
        }

        void Follow()
        {
            if (race == null) return;
            data.Sample(race.Elapsed, out var position, out var rotation);
            transform.SetPositionAndRotation(position, rotation);
        }

        void LateUpdate() => Follow();
    }
}
