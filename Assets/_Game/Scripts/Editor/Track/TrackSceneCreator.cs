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
