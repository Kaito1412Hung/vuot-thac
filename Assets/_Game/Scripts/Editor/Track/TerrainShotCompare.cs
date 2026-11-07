using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Chụp ảnh cố định ở các cửa sông và lối hang trên núi, rồi so từng điểm ảnh giữa hai lần chụp.
    /// Dùng để chắc chắn một thay đổi về lưới không làm map trông khác đi.
    /// </summary>
    public static class TerrainShotCompare
    {
        public static List<(string name, Vector3 eye, Vector3 target)> Views(RiverTrack track)
        {
            var views = new List<(string, Vector3, Vector3)>();
            int n = 0;
            foreach (var b in track.Branches)
                foreach (float d in new[] { 0f, b.Length })
                {
                    var s = b.EvaluateAtDistance(d);
                    var p = s.center; p.x += b.side * track.appliedRaceStripWidth * .5f;
                    var fwd = s.tangent; fwd.y = 0; fwd.Normalize();
                    var side = Vector3.Cross(Vector3.up, fwd);
                    views.Add(($"mouth{n}_a", p - fwd * 18 + side * 10 + Vector3.up * 7, p));
                    views.Add(($"mouth{n}_b", p + fwd * 18 - side * 10 + Vector3.up * 5, p));
                    n++;
                }
            var access = track.transform.Find("Generated/MountainCaveAccess");
            if (access != null)
                foreach (var r in access.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.EndsWith("_Interior")))
                {
                    var c = r.bounds.center; float e = r.bounds.extents.magnitude;
                    views.Add(($"pass_{r.name}", c + new Vector3(e * .9f, e * .8f, -e * .9f), c));
                }
            return views;
        }

        public static string Capture(RiverTrack track, string folder)
        {
            Directory.CreateDirectory(folder);
            var go = new GameObject("ShotCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60; cam.nearClipPlane = .2f; cam.farClipPlane = 600;
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            int count = 0;
            try
            {
                foreach (var v in Views(track))
                {
                    go.transform.position = v.eye; go.transform.LookAt(v.target);
                    cam.Render();
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(folder, v.name + ".png"), tex.EncodeToPNG());
                    count++;
                }
            }
            finally { cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); }
            return $"{count} ảnh -> {folder}";
        }

        /// <summary>So hai thư mục ảnh: số điểm ảnh lệch quá ngưỡng và độ lệch lớn nhất.</summary>
        public static string Compare(string before, string after, int threshold = 12)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var file in Directory.GetFiles(before, "*.png").OrderBy(f => f))
            {
                string other = Path.Combine(after, Path.GetFileName(file));
                if (!File.Exists(other)) { sb.AppendLine(Path.GetFileName(file) + ": thiếu"); continue; }
                var a = new Texture2D(2, 2); a.LoadImage(File.ReadAllBytes(file));
                var b = new Texture2D(2, 2); b.LoadImage(File.ReadAllBytes(other));
                var pa = a.GetPixels32(); var pb = b.GetPixels32();
                int diff = 0, max = 0;
                for (int i = 0; i < pa.Length; i++)
                {
                    int d = Mathf.Max(Mathf.Abs(pa[i].r - pb[i].r), Mathf.Max(Mathf.Abs(pa[i].g - pb[i].g), Mathf.Abs(pa[i].b - pb[i].b)));
                    if (d > threshold) diff++;
                    if (d > max) max = d;
                }
                sb.AppendLine($"{Path.GetFileName(file)}: lệch {diff * 100f / pa.Length:0.000}% điểm ảnh, lớn nhất {max}");
                Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            }
            return sb.ToString();
        }
    }
}
