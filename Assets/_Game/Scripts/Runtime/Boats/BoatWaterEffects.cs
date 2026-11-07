using System.Collections.Generic;
using UnityEngine;

namespace VuotThac.Boats
{
    /// <summary>
    /// Những thứ làm thuyền trông như thật sự nằm trong nước:
    /// dải bọt viền quanh thân ở đường nước, vệt nước rẽ sau đuôi, bọt tung ở mũi khi chạy nhanh,
    /// nước bắn và vòng gợn sóng mỗi lần mái chèo chạm nước (BoatRower gọi <see cref="Splash"/>).
    /// Đường nước tính một lần lúc bắt đầu bằng cách cắt lưới thân thuyền tại mặt nước.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class BoatWaterEffects : MonoBehaviour
    {
        [Tooltip("Vật liệu VuotThac/WaterFx kiểu Dot (hạt bọt, giọt nước)")] public Material dot;
        [Tooltip("Vật liệu VuotThac/WaterFx kiểu Ring (vòng gợn sóng)")] public Material ring;
        [Tooltip("Vật liệu VuotThac/WaterFx kiểu Band (dải bọt viền thân)")] public Material band;
        [Range(0, 2)] public float intensity = 1f;

        BoatController boat;
        Rigidbody rb;
        Transform foam;
        Mesh foamMesh;
        Vector2[] outline, normals;
        Vector3[] foamVertices;
        float foamWidth = -1f, sternZ, bowZ, sternHalf, bowHalf, wakeDistance, sprayTimer;
        ParticleSystem wake, droplets, ripples;
        Vector3 lastPosition;
        static readonly Color FoamColor = new(0.93f, 0.97f, 1f, 1f);

        public float WaterHeight { get; private set; }

        bool initialized;
        void Awake() => Initialize();

        /// <summary>Dựng dải bọt và hệ hạt. Gọi được cả trong Editor (công cụ chụp thuyền) khi Awake chưa chạy.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            boat = GetComponent<BoatController>();
            rb = GetComponent<Rigidbody>();
            BuildOutline();
            if (band != null) BuildFoam();
            if (dot != null)
            {
                wake = MakeSystem("Wake", dot, ParticleSystemRenderMode.HorizontalBillboard, 900, 0f);
                droplets = MakeSystem("Droplets", dot, ParticleSystemRenderMode.Billboard, 500, 1f);
            }
            if (ring != null) ripples = MakeSystem("Ripples", ring, ParticleSystemRenderMode.HorizontalBillboard, 120, 0f);
            lastPosition = transform.position;
        }

        // ---------- Đường nước ----------

        void BuildOutline()
        {
            var points = new List<Vector2>();
            float waterY = boat != null ? -boat.FloatHeight : 0f;
            var filter = boat != null && boat.model != null ? boat.model.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
            {
                var v = mesh.vertices; var t = mesh.triangles;
                var m = filter.transform.localToWorldMatrix; var toBoat = transform.worldToLocalMatrix * m;
                for (int i = 0; i < t.Length; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = toBoat.MultiplyPoint3x4(v[t[i + e]]), b = toBoat.MultiplyPoint3x4(v[t[i + (e + 1) % 3]]);
                        if ((a.y - waterY) * (b.y - waterY) > 0f || Mathf.Approximately(a.y, b.y)) continue;
                        var p = Vector3.Lerp(a, b, (waterY - a.y) / (b.y - a.y));
                        points.Add(new Vector2(p.x, p.z));
                    }
            }
            if (points.Count < 3)
            {
                float hl = boat != null ? boat.hullLength * 0.5f : 2f, hb = boat != null ? boat.hullBeam * 0.5f : 0.5f;
                for (int i = 0; i < 32; i++) { float a = i * Mathf.PI * 2 / 32; points.Add(new Vector2(Mathf.Cos(a) * hb, Mathf.Sin(a) * hl)); }
            }
            var hull = ConvexHull(points);
            outline = Resample(hull, 72);
            normals = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                var d = outline[(i + 1) % outline.Length] - outline[(i - 1 + outline.Length) % outline.Length];
                normals[i] = new Vector2(d.y, -d.x).normalized; // vỏ lồi đi ngược chiều kim đồng hồ: pháp tuyến hướng ra ngoài
            }
            sternZ = float.MaxValue; bowZ = float.MinValue;
            foreach (var p in outline) { sternZ = Mathf.Min(sternZ, p.y); bowZ = Mathf.Max(bowZ, p.y); }
            sternHalf = HalfWidthAt(Mathf.Lerp(sternZ, bowZ, 0.12f)); bowHalf = HalfWidthAt(Mathf.Lerp(sternZ, bowZ, 0.85f));
        }

        float HalfWidthAt(float z)
        {
            float w = 0;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length];
                if ((a.y - z) * (b.y - z) > 0 || Mathf.Approximately(a.y, b.y)) continue;
                w = Mathf.Max(w, Mathf.Abs(Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y))));
            }
            return w;
        }

        static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int k = 0; k < pts.Count; k++)
                {
                    var p = pass == 0 ? pts[k] : pts[pts.Count - 1 - k];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h;
        }

        static Vector2[] Resample(List<Vector2> loop, int count)
        {
            float total = 0; for (int i = 0; i < loop.Count; i++) total += Vector2.Distance(loop[i], loop[(i + 1) % loop.Count]);
            var result = new Vector2[count]; int seg = 0; float segStart = 0;
            for (int k = 0; k < count; k++)
            {
                float target = total * k / count;
                while (true)
                {
                    float len = Vector2.Distance(loop[seg], loop[(seg + 1) % loop.Count]);
                    if (segStart + len >= target || seg == loop.Count - 1) { result[k] = Vector2.Lerp(loop[seg], loop[(seg + 1) % loop.Count], len > 0 ? (target - segStart) / len : 0); break; }
                    segStart += len; seg++;
                }
            }
            return result;
        }

        // ---------- Dải bọt viền ----------

        void BuildFoam()
        {
            var go = new GameObject("ContactFoam");
            go.transform.SetParent(transform, false);
            foam = go.transform;
            foamMesh = new Mesh { name = "ContactFoam" };
            int n = outline.Length;
            foamVertices = new Vector3[n * 2];
            var uv = new Vector2[n * 2]; var colors = new Color[n * 2]; var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                uv[i * 2] = new Vector2(i / (float)n, 0); uv[i * 2 + 1] = new Vector2(i / (float)n, 1);
                colors[i * 2] = colors[i * 2 + 1] = FoamColor;
                int j = (i + 1) % n;
                tris[i * 6] = i * 2; tris[i * 6 + 1] = j * 2; tris[i * 6 + 2] = i * 2 + 1;
                tris[i * 6 + 3] = j * 2; tris[i * 6 + 4] = j * 2 + 1; tris[i * 6 + 5] = i * 2 + 1;
            }
            SetFoamWidth(0.22f);
            foamMesh.vertices = foamVertices; foamMesh.uv = uv; foamMesh.colors = colors; foamMesh.triangles = tris;
            foamMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20);
            go.AddComponent<MeshFilter>().sharedMesh = foamMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = band; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        void SetFoamWidth(float width)
        {
            if (Mathf.Abs(width - foamWidth) < 0.01f) return;
            foamWidth = width;
            for (int i = 0; i < outline.Length; i++)
            {
                var p = outline[i]; var nrm = normals[i];
                // Mũi thuyền rẽ nước nên bọt dày hơn ở phía trước
                float bow = Mathf.InverseLerp(Mathf.Lerp(sternZ, bowZ, 0.55f), bowZ, p.y);
                float w = width * (1f + bow * 1.4f);
                foamVertices[i * 2] = new Vector3(p.x - nrm.x * 0.05f, 0, p.y - nrm.y * 0.05f);
                foamVertices[i * 2 + 1] = new Vector3(p.x + nrm.x * w, 0, p.y + nrm.y * w);
            }
            if (foamMesh != null) { foamMesh.vertices = foamVertices; }
        }

        // ---------- Hệ hạt ----------

        ParticleSystem MakeSystem(string name, Material material, ParticleSystemRenderMode mode, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false; main.loop = true; main.maxParticles = max; main.gravityModifier = gravity;
            main.startLifetime = 1f; main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, mode == ParticleSystemRenderMode.Billboard ? AnimationCurve.Linear(0, 1, 1, 0.4f) : AnimationCurve.EaseInOut(0, 0.35f, 1, 1f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode; r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.World;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float life, float alpha)
        {
            if (ps == null) return;
            var e = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size, startLifetime = life,
                startColor = new Color(FoamColor.r, FoamColor.g, FoamColor.b, Mathf.Clamp01(alpha * intensity)),
                rotation = Random.Range(0f, 360f), applyShapeToPosition = false
            };
            ps.Emit(e, 1);
        }

        /// <summary>Nước bắn khi mái chèo (hoặc sào) cắt mặt nước. <paramref name="strength"/> từ 0 đến 1.</summary>
        public void Splash(Vector3 point, Vector3 bladeVelocity, float strength)
        {
            point.y = WaterHeight + 0.02f;
            int drops = Mathf.RoundToInt(Mathf.Lerp(3, 14, strength));
            for (int i = 0; i < drops; i++)
            {
                var v = Random.insideUnitSphere * 0.9f; v.y = Mathf.Abs(v.y) * 1.6f + 0.9f * strength + 0.3f;
                v += Vector3.ProjectOnPlane(bladeVelocity, Vector3.up) * 0.25f;
                Emit(droplets, point + Random.insideUnitSphere * 0.06f, v, Random.Range(0.035f, 0.08f), Random.Range(0.35f, 0.7f), 0.9f);
            }
            Emit(ripples, point, Vector3.zero, Mathf.Lerp(0.35f, 0.8f, strength), Random.Range(1.1f, 1.6f), 0.55f + strength * 0.3f);
            Emit(wake, point, Vector3.zero, Mathf.Lerp(0.18f, 0.4f, strength), Random.Range(0.8f, 1.3f), 0.5f * strength + 0.2f);
        }

        /// <summary>Gợn nhẹ khi mái chèo đang kéo trong nước.</summary>
        public void Swirl(Vector3 point, float strength)
        {
            point.y = WaterHeight + 0.02f;
            Emit(wake, point + Random.insideUnitSphere * 0.04f, Vector3.zero, Random.Range(0.12f, 0.25f) * (0.5f + strength), Random.Range(0.7f, 1.2f), 0.35f * strength + 0.1f);
        }

        // ---------- Mỗi khung hình ----------

        void LateUpdate() => Tick();

        public void Tick()
        {
            if (!initialized) Initialize();
            bool kinematic = rb == null || rb.isKinematic;
            var info = boat != null ? boat.Water : default;
            WaterHeight = !kinematic && info.inWater ? info.height : transform.position.y - (boat != null ? boat.FloatHeight : 0f);
            bool inWater = kinematic || info.inWater;

            Vector3 velocity = kinematic ? Vector3.zero : rb.linearVelocity;
            Vector3 relative = velocity - (kinematic ? Vector3.zero : info.flow); relative.y = 0;
            float speed = relative.magnitude;

            var fwd = transform.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);

            if (foam != null)
            {
                foam.gameObject.SetActive(inWater);
                foam.SetPositionAndRotation(new Vector3(transform.position.x, WaterHeight + 0.012f, transform.position.z), Quaternion.LookRotation(fwd, Vector3.up));
                SetFoamWidth(Mathf.Lerp(0.16f, 0.5f, Mathf.Clamp01(speed / 9f)));
            }
            if (!inWater || kinematic) { lastPosition = transform.position; return; }

            Vector3 Local(float x, float z) { var p = transform.position + right * x + fwd * z; p.y = WaterHeight + 0.015f; return p; }

            // Vệt nước sau đuôi: thả bọt theo quãng đường đi được, toả ra hai bên
            wakeDistance += Vector3.Distance(transform.position, lastPosition);
            lastPosition = transform.position;
            float spacing = 0.15f; // thả dày để các mảng bọt chồng lên nhau thành dải liền
            while (wakeDistance > spacing && speed > 0.6f)
            {
                wakeDistance -= spacing;
                float k = Mathf.Clamp01(speed / 9f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = Local(side * sternHalf * 0.9f, sternZ + 0.05f);
                    var spread = right * side * Mathf.Lerp(0.3f, 1.3f, k) + velocity * 0.15f;
                    Emit(wake, p, spread, Mathf.Lerp(0.35f, 0.85f, k), Mathf.Lerp(1.4f, 2.8f, k), Mathf.Lerp(0.3f, 0.6f, k));
                    // Sóng mũi tách ra hai bên
                    var b = Local(side * bowHalf, bowZ - 0.25f);
                    Emit(wake, b, right * side * Mathf.Lerp(0.4f, 1.6f, k), Mathf.Lerp(0.25f, 0.6f, k), Mathf.Lerp(0.8f, 1.6f, k), Mathf.Lerp(0.2f, 0.5f, k));
                }
                Emit(wake, Local(0, sternZ - 0.1f), velocity * 0.1f, Mathf.Lerp(0.45f, 1f, k), Mathf.Lerp(1.6f, 3f, k), Mathf.Lerp(0.25f, 0.5f, k));
            }
            wakeDistance = Mathf.Min(wakeDistance, spacing * 3);

            // Bọt tung ở mũi khi chạy nhanh
            if (speed > 4f)
            {
                sprayTimer -= Time.deltaTime * (speed - 3f) * 6f;
                while (sprayTimer < 0f)
                {
                    sprayTimer += 1f;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    var p = Local(side * bowHalf * 0.8f, bowZ - 0.15f);
                    var v = right * side * Random.Range(0.6f, 1.6f) + Vector3.up * Random.Range(0.8f, 1.8f) + velocity * 0.6f;
                    Emit(droplets, p, v, Random.Range(0.04f, 0.1f), Random.Range(0.4f, 0.7f), 0.8f);
                }
            }
        }
    }
}
