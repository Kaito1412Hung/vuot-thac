using System.IO;
using UnityEngine;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    /// <summary>
    /// Chụp từng thuyền đặt trên mặt nước ở vạch xuất phát: góc 3/4, góc sát mặt nước và góc nhìn thứ nhất.
    /// Dùng để so trước/sau khi chỉnh hình thuyền, độ chìm và động tác chèo.
    /// </summary>
    public static class BoatShowcase
    {
        public static string Capture(string folder, float phase = 0.15f, float effort = 0.8f)
        {
            Directory.CreateDirectory(folder);
            var track = Object.FindFirstObjectByType<RiverTrack>();
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatCatalog>("Assets/_Game/Data/BoatCatalog.asset");
            var s = track.EvaluateAtDistance(track.StartDistance + 30f);
            var spot = track.AfterRaceInsertion(s.center); spot.y = s.WaterHeight;
            var fwd = s.tangent; fwd.y = 0; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);

            var camGo = new GameObject("ShowcaseCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            int shots = 0;
            void Shot(string name, Vector3 eye, Vector3 look, float fov, float near)
            {
                cam.fieldOfView = fov; cam.nearClipPlane = near; cam.farClipPlane = 800;
                camGo.transform.position = eye; camGo.transform.LookAt(look);
                cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
                shots++;
            }
            try
            {
                foreach (var def in catalog.boats)
                {
                    var boat = Object.Instantiate(def.prefab);
                    boat.hideFlags = HideFlags.DontSave;
                    var controller = boat.GetComponent<BoatController>();
                    boat.transform.SetPositionAndRotation(spot + Vector3.up * controller.FloatHeight, Quaternion.LookRotation(fwd, Vector3.up));
                    var rower = boat.GetComponentInChildren<BoatRower>();
                    float len = controller.hullLength;
                    if (rower != null) { rower.Capture(); rower.Pose(phase, effort); }
                    var fx = boat.GetComponent<BoatWaterEffects>();
                    if (fx != null)
                    {
                        boat.GetComponent<Rigidbody>().isKinematic = true; // như thuyền ở màn chọn: bọt viền hiện, không có vệt nước
                        fx.Initialize(); fx.Tick();
                        // Nước bắn ở chỗ lưỡi chèo cắt mặt nước, cho chạy hạt một chút để thấy giọt nước bay lên
                        foreach (var r in boat.GetComponentsInChildren<BoatRower>())
                        {
                            if (r.paddle == null) continue;
                            var mf = r.paddle.GetComponent<MeshFilter>(); if (mf == null) continue;
                            var a = r.paddle.TransformPoint(0, mf.sharedMesh.bounds.min.y, 0); var b = r.paddle.TransformPoint(0, mf.sharedMesh.bounds.max.y, 0);
                            foreach (var (lo, hi) in new[] { (a, b), (b, a) })
                                if (lo.y < fx.WaterHeight && hi.y > fx.WaterHeight)
                                    fx.Splash(Vector3.Lerp(hi, lo, (hi.y - fx.WaterHeight) / (hi.y - lo.y)), Vector3.zero, 0.9f);
                        }
                        foreach (var ps in boat.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(0.18f, true, false);
                    }
                    Shot(def.name + "_1_ba_phan_tu", spot + fwd * len * .9f + right * len * .9f + Vector3.up * len * .45f, spot + Vector3.up * .3f, 40, .1f);
                    Shot(def.name + "_2_sat_mat_nuoc", spot + right * len * 1.1f + fwd * len * .25f + Vector3.up * .35f, spot + Vector3.up * .2f, 40, .1f);
                    if (rower != null && rower.eye != null)
                    {
                        rower.SetFirstPerson(true);
                        rower.Pose(phase, effort);
                        var eye = rower.eye.position;
                        var look = eye + Quaternion.AngleAxis(19, boat.transform.right) * boat.transform.forward * 5;
                        Shot(def.name + "_3_goc_nhin_1", eye, look, 70, .04f);
                        rower.SetFirstPerson(false);
                    }
                    Object.DestroyImmediate(boat);
                }
            }
            finally { cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(camGo); }
            return $"{shots} ảnh -> {folder}";
        }
    }
}
