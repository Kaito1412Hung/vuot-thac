using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    /// <summary>Tạo scene cho từng đường đua, gồm spline sông, ánh sáng, sương mù và camera xem thử.</summary>
    public static class TrackSceneCreator
    {
        const string SceneFolder = "Assets/_Game/Scenes";
        const string Track01Path = SceneFolder + "/Track01_SuoiRung.unity";

        // Đất hai bên sông rộng 140 m nên núi ở khá xa. Sương mù và tầm nhìn đủ xa để còn thấy núi,
        // nhưng không quá xa để điện thoại khỏi phải vẽ quá nhiều
        public const float FogStart = 90f;
        public const float FogEnd = 340f;
        public const float FarClip = 380f;

        // Đường 1 - Suối Rừng: nước êm, cua lượn chữ S, đổ dốc nhẹ khoảng 8 m.
        // Điểm đầu và điểm cuối là đoạn sông kéo dài ngoài đường đua.
        static readonly Vector3[] Track01Knots =
        {
            new(0f, 8f, -60f),
            new(0f, 8f, 0f),
            new(5f, 7.8f, 60f),
            new(30f, 7.2f, 115f),
            new(62f, 6.6f, 160f),
            new(70f, 6f, 225f),
            new(48f, 5.3f, 285f),
            new(12f, 4.6f, 325f),
            new(-22f, 4f, 375f),
            new(-35f, 3.4f, 440f),
            new(-20f, 2.8f, 505f),
            new(12f, 2.2f, 550f),
            new(45f, 1.6f, 600f),
            new(60f, 1f, 665f),
            new(52f, 0.5f, 730f),
            new(40f, 0f, 790f),
            new(32f, -0.3f, 850f),
        };

        [MenuItem("Vượt Thác/Tạo scene Đường 1 - Suối Rừng")]
        public static void CreateTrack01()
        {
            if (File.Exists(Track01Path))
            {
                Debug.LogWarning($"[Vượt Thác] Scene {Track01Path} đã có. Mở scene đó rồi dùng nút dựng lại trên RiverTrack, " +
                                 "hoặc xóa file scene nếu muốn tạo lại từ đầu.");
                EditorSceneManager.OpenScene(Track01Path);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var river = new GameObject("River_SuoiRung");
            var container = river.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();
            foreach (var k in Track01Knots)
                spline.Add((float3)k, TangentMode.AutoSmooth);
            var track = river.AddComponent<RiverTrack>();

            SetupLighting();
            EditorSceneManager.SaveScene(scene, Track01Path);

            RiverTrackBuilder.Build(track);
            PlaceCamera(track);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(Track01Path);

            Selection.activeGameObject = river;
        }

        /// <summary>
        /// Gắn 3 thác nước và 3 hang động xuyên núi cho Đường 1 rồi dựng lại map.
        /// Vị trí chọn để tránh cầu, làng và chỗ rẽ của các nhánh sông.
        /// </summary>
        [MenuItem("Vượt Thác/Đặt thác và hầm cho Đường 1")]
        public static void SetupTrack01Features()
        {
            var track = Object.FindAnyObjectByType<RiverTrack>();
            if (track == null)
            {
                Debug.LogWarning("[Vượt Thác] Scene đang mở không có RiverTrack nào.");
                return;
            }

            Undo.RecordObject(track, "Đặt thác và hầm");
            track.falls = new System.Collections.Generic.List<RiverFall>
            {
                new() { distance = 150f, height = 4f, length = 9f },
                new() { distance = 575f, height = 5f, length = 10f },
                new() { distance = 760f, height = 4.5f, length = 9f },
            };
            track.tunnels = new System.Collections.Generic.List<RiverTunnel>
            {
                // Đặt lệch khỏi các suối từ thác núi (mét 172, 363, 562, 769) và chỗ nhánh đường vòng nhập lại (mét 850)
                new() { distance = 480f, length = 56f, ridgeHeight = 50f, ceilingHeight = 13f },
                new() { distance = 890f, length = 40f, ridgeHeight = 40f, ceilingHeight = 11f },
            };

            // Ngay sau xuất phát, sông chính bị gỗ và đá chặn lại. Người chơi chọn rẽ trái hoặc phải,
            // chạy vào dãy núi cao nhất, chui qua hang dài trong lòng núi rồi vòng ra nhập lại sông chính.
            track.barriers = new System.Collections.Generic.List<float> { 128f };
            var longRoute = track.branches?.Find(b => b != null && b.fromDistance > 600f);
            track.branches = new System.Collections.Generic.List<RiverBranch>
            {
                new()
                {
                    name = "Hang Núi Trái", fromDistance = 105f, toDistance = 400f, side = -1, width = 12f, depth = 1.5f,
                    // Bờ trái nằm phía trong khúc cua lớn nên dù phình xa, đường vẫn ngắn (khoảng 320 m), nước chảy chậm hơn
                    bulge = 165f, wiggle = 6f, minLandGap = 10f, rocksPer100m = 2f, flowMultiplier = 1.15f, signColor = 0, caveCeiling = 9f,
                    // Chỗ này chỉ là đồi thấp, đắp thêm núi để hang nằm hẳn trong lòng núi
                    mountainHeight = 30f,
                },
                new()
                {
                    name = "Hang Núi Phải", fromDistance = 105f, toDistance = 400f, side = 1, width = 12f, depth = 1.5f,
                    // Bờ phải nằm phía ngoài khúc cua nên chỉ phình vừa phải cho hai đường dài gần bằng nhau.
                    // Chỗ đó chưa có núi cao nên đắp thêm một dãy núi ngay trên nhánh để có hang chui qua.
                    bulge = 60f, wiggle = -15f, minLandGap = 10f, rocksPer100m = 3f, flowMultiplier = 1.5f, signColor = 1, caveCeiling = 9f,
                    mountainHeight = 38f,
                },
                longRoute ?? new()
                {
                    name = "Đường vòng Rừng Thông", fromDistance = 665f, toDistance = 850f, side = 1, width = 12f, depth = 1.5f,
                    bulge = 50f, wiggle = 6f, minLandGap = 10f, rocksPer100m = 2f, signColor = 1,
                },
            };
            EditorUtility.SetDirty(track);
            track.InvalidateCache();
            RiverTrackBuilder.Build(track);
            EditorSceneManager.SaveScene(track.gameObject.scene);
        }

        static void SetupLighting()
        {
            var sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.name = "Sun";
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
            }

            // Sương mù che chỗ địa hình kết thúc và giúp điện thoại không phải vẽ quá xa
            var fogColor = new Color(0.74f, 0.85f, 0.92f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.skybox = LoadOrCreateSky(fogColor);
        }

        /// <summary>
        /// Bầu trời có nửa dưới cùng màu sương mù, để chỗ camera không vẽ tới (ngoài tầm nhìn xa)
        /// hòa vào sương thay vì lộ ra một dải màu xám.
        /// </summary>
        [MenuItem("Vượt Thác/Áp dụng bầu trời cho scene đang mở")]
        public static void ApplySkyToOpenScene()
        {
            RenderSettings.skybox = LoadOrCreateSky(RenderSettings.fogColor);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static Material LoadOrCreateSky(Color groundColor)
        {
            const string path = TrackMaterials.Folder + "/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                Directory.CreateDirectory(TrackMaterials.Folder);
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky" };
                AssetDatabase.CreateAsset(sky, path);
            }

            sky.SetColor("_GroundColor", groundColor);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.62f, 0.8f));
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        static void PlaceCamera(RiverTrack track)
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var s = track.EvaluateAtDistance(track.StartDistance);
            cam.transform.position = s.center - s.tangent * 14f + Vector3.up * 6f;
            cam.transform.LookAt(s.center + s.tangent * 25f + Vector3.up);
            cam.farClipPlane = FarClip;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;

            // Bỏ scene mẫu của template, đường đua đầu tiên làm scene mở đầu
            scenes.RemoveAll(s => s.path == "Assets/Scenes/SampleScene.unity");
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
