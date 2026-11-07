#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    /// <summary>
    /// Chỉ dùng để thử trong Play Mode ở Editor (không có trong bản build): lái tự động bám giữa dòng và chụp ảnh từ camera game.
    /// </summary>
    public class BoatPlayTester : MonoBehaviour
    {
        public float throttle = 1f, lookAhead = 14f;
        PlayerBoatInput input;
        BoatController boat;
        RiverTrack track;

        void Update()
        {
            if (boat == null) { boat = GetComponent<BoatController>(); input = GetComponent<PlayerBoatInput>(); }
            track = boat != null ? boat.Track : null;
            if (input == null || track == null) return;
            var near = track.GetNearest(track.BeforeRaceInsertion(transform.position));
            var target = track.AfterRaceInsertion(track.EvaluateAtDistance(near.distance + lookAhead).center);
            var to = target - transform.position; to.y = 0;
            var fwd = transform.forward; fwd.y = 0;
            input.TouchSteer = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up) / 25f, -1f, 1f);
            input.TouchThrottle = throttle;
        }

        public static string Begin(int boatIndex)
        {
            var screen = Object.FindAnyObjectByType<BoatSelectScreen>();
            var f = typeof(BoatSelectScreen).GetField("index", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            f.SetValue(screen, boatIndex);
            typeof(BoatSelectScreen).GetMethod("StartRace", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(screen, null);
            var player = Object.FindAnyObjectByType<PlayerBoatInput>();
            player.gameObject.AddComponent<BoatPlayTester>();
            return player.name;
        }

        public static string Shot(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(rt); Object.Destroy(tex);
            return path;
        }

        public static string State()
        {
            var b = Object.FindAnyObjectByType<PlayerBoatInput>().GetComponent<BoatController>();
            var w = b.Water;
            var fx = b.GetComponent<BoatWaterEffects>();
            int particles = 0; foreach (var ps in b.GetComponentsInChildren<ParticleSystem>()) particles += ps.particleCount;
            return $"{b.name} speed={b.Speed:0.0} m/s up={b.transform.up.y:0.000} origin-water={b.transform.position.y - w.height:0.000} (FloatHeight {b.FloatHeight:0.00}) inWater={w.inWater} particles={particles} fps={1f / Time.smoothDeltaTime:0}";
        }
    }
}
#endif
