using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Mesh đồ trang trí low-poly tự sinh bằng code, lưu chung trong một file asset.
    /// Đây là đồ tạm cho các bản map đầu tiên, sau này có thể thay bằng asset miễn phí của Kenney hoặc Quaternius.
    ///
    /// Quy ước submesh của từng mẫu (mỗi submesh dùng một vật liệu):
    /// Tree: 0 thân, 1 lá. Bush, Cloud, LilyPad: 0. LilyPadFlower: 0 lá, 1 hoa. Flowers: 0 cuống, 1 cánh hoa.
    /// Reeds: 0 lá sậy, 1 bông sậy. Log: 0 vỏ, 1 mặt cắt.
    /// Cabin, HouseTall, Hut, Well: 0 tường, 1 gỗ tối, 2 mái, 3 đá. Dock, Bridge, MillWheel: 0 gỗ, 1 gỗ tối.
    /// Fence, Haystack: 0. Sign: 0 cột, 1 bảng, 2 mũi tên. Thú rừng: xem phần "Thú rừng" bên dưới.
    /// </summary>
    public class PropLibrary
    {
        public const string AssetPath = "Assets/_Game/Generated/Props.asset";

        public enum TreeKind { Pine, PineTall, Round }

        static readonly string[] TreeNames = { "Tree_Pine", "Tree_PineTall", "Tree_Round" };

        static readonly string[] AllNames =
        {
            "Rock_0", "Rock_1", "Rock_2", "Rock_3",
            "Tree_Pine", "Tree_PineTall", "Tree_Round",
            "Bush_0", "Bush_1", "Flowers", "Reeds", "LilyPad", "LilyPadFlower", "Log",
            "Cloud_0", "Cloud_1",
            "Cabin", "Dock", "Sign", "HouseTall", "Hut", "Fence", "Well", "Haystack", "MillWheel",
            "Deer_Body", "Deer_Leg", "Fox_Body", "Fox_Leg", "Bear_Body", "Bear_Leg", "Rabbit", "Duck",
            "Bird_Body", "Bird_Wing",
        };

        readonly Dictionary<string, Mesh> meshes = new();

        public Mesh this[string name] => meshes[name];
        public Mesh[] Rocks => new[] { this["Rock_0"], this["Rock_1"], this["Rock_2"], this["Rock_3"] };
        public Mesh[] Bushes => new[] { this["Bush_0"], this["Bush_1"] };
        public Mesh[] Clouds => new[] { this["Cloud_0"], this["Cloud_1"] };
        public Mesh Tree(TreeKind kind) => this[TreeNames[(int)kind]];

        public static PropLibrary LoadOrCreate()
        {
            var lib = new PropLibrary();
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<Mesh>())
                lib.meshes[m.name] = m;

            if (AllNames.All(lib.meshes.ContainsKey))
                return lib;

            // Thiếu mẫu nào thì tạo lại cả file cho đồng bộ
            if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                AssetDatabase.DeleteAsset(AssetPath);
            return Create();
        }

        public static void RefreshDetailedMeshes()
        {
            var replacements = new[] { BuildPine("Tree_Pine", 1f, 3, 77), BuildPine("Tree_PineTall", 1.35f, 4, 91), BuildRoundTree("Tree_Round", 123), BuildCabin("Cabin"), BuildHouseTall("HouseTall"), BuildFlowers("Flowers", 42) };
            replacements = replacements.Concat(NaturalAnimalMeshes.BuildAll()).ToArray();
            foreach (var fresh in replacements)
            {
                var existing = AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<Mesh>().FirstOrDefault(m => m.name == fresh.name);
                if (existing != null) { existing.Clear(); existing.indexFormat = fresh.indexFormat; existing.vertices = fresh.vertices; existing.normals = fresh.normals; existing.uv = fresh.uv; existing.subMeshCount = fresh.subMeshCount; for (int i = 0; i < fresh.subMeshCount; i++) existing.SetTriangles(fresh.GetTriangles(i), i); existing.RecalculateBounds(); EditorUtility.SetDirty(existing); Object.DestroyImmediate(fresh); }
                else AssetDatabase.AddObjectToAsset(fresh, AssetPath);
            }
            AssetDatabase.SaveAssets();
        }

        static PropLibrary Create()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            var list = new List<Mesh>();
            for (int i = 0; i < 4; i++)
                list.Add(BuildRock(i, 9001 + i * 31));
            list.Add(BuildPine("Tree_Pine", 1f, 3, 77));
            list.Add(BuildPine("Tree_PineTall", 1.35f, 4, 91));
            list.Add(BuildRoundTree("Tree_Round", 123));
            list.Add(BuildBush("Bush_0", 501));
            list.Add(BuildBush("Bush_1", 777));
            list.Add(BuildFlowers("Flowers", 42));
            list.Add(BuildReeds("Reeds", 64));
            list.Add(BuildLilyPad("LilyPad", false));
            list.Add(BuildLilyPad("LilyPadFlower", true));
            list.Add(BuildLog("Log"));
            list.Add(BuildCloud("Cloud_0", 11));
            list.Add(BuildCloud("Cloud_1", 29));
            list.Add(BuildCabin("Cabin"));
            list.Add(BuildDock("Dock"));
            list.Add(BuildSign("Sign"));
            list.Add(BuildHouseTall("HouseTall"));
            list.Add(BuildHut("Hut"));
            list.Add(BuildFence("Fence"));
            list.Add(BuildWell("Well"));
            list.Add(BuildHaystack("Haystack"));
            list.Add(BuildMillWheel("MillWheel"));
            list.AddRange(NaturalAnimalMeshes.BuildAll());

            // Asset chính bị Unity đổi tên theo tên file, nên dùng một mesh rỗng làm asset chính
            // để giữ nguyên tên các mẫu thật
            AssetDatabase.CreateAsset(new Mesh { name = "Props" }, AssetPath);
            foreach (var m in list)
                AssetDatabase.AddObjectToAsset(m, AssetPath);
            AssetDatabase.SaveAssets();

            var lib = new PropLibrary();
            foreach (var m in list)
                lib.meshes[m.name] = m;
            return lib;
        }

        // ---------- Đá ----------

        static Mesh BuildRock(int index, int seed)
        {
            var rng = new System.Random(seed);
            Icosphere(1, out var verts, out var tris);
            for (int i = 0; i < verts.Count; i++)
            {
                float r = 0.75f + (float)rng.NextDouble() * 0.4f;
                Vector3 v = verts[i] * r;
                v.y *= 0.75f;
                verts[i] = v;
            }

            var fb = new FlatMeshBuilder(1);
            for (int i = 0; i < tris.Count; i += 3)
                fb.TriOutward(0, verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], Vector3.zero);
            return fb.ToMesh($"Rock_{index}");
        }

        // ---------- Cây, bụi, hoa, sậy ----------

        static Mesh BuildPine(string name, float heightScale, int tiers, int seed)
        {
            var rng = new System.Random(seed);
            var fb = new FlatMeshBuilder(2);

            float height = 5.8f * heightScale;
            // One continuous crown: no stacked leaf discs or exposed pole at the top.
            Prism(fb, 0, new Vector3(0, -0.3f, 0), 0.18f, height * 0.78f, 9);
            const int rings = 18, sides = 13;
            var points = new Vector3[rings, sides];
            var crownCenter = Vector3.up * height * 0.55f;
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float radius = 1.65f * Mathf.Pow(1f - t, 0.85f);
                // Small drooping branch tips break up the outline without separate cones.
                radius *= 1f + 0.075f * Mathf.Sin(r * 2.3f);
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    float irregular = 1f + 0.09f * Mathf.Sin(j * 2.7f + seed) + 0.04f * Mathf.Sin(r + j);
                    points[r, j] = new Vector3(Mathf.Cos(angle) * radius * irregular,
                        height * (0.22f + t * 0.8f) + (1f - t) * 0.08f * Mathf.Sin(j * 3.1f),
                        Mathf.Sin(angle) * radius * irregular);
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    // Wind each side consistently. A single interior point flips faces on
                    // concave rings, making strips disappear with backface culling.
                    fb.Tri(1, points[r,j], points[r+1,j], points[r+1,k]);
                    if (r < rings - 2)
                        fb.Tri(1, points[r,j], points[r+1,k], points[r,k]);
                }
            for (int j = 0; j < sides; j++)
                fb.TriOutward(1, Vector3.up * height * 0.22f, points[0,j], points[0,(j+1)%sides], crownCenter);
            return fb.ToMesh(name);
        }

        static Mesh BuildRoundTree(string name, int seed)
        {
            var rng = new System.Random(seed);
            var fb = new FlatMeshBuilder(2);
            Prism(fb, 0, new Vector3(0, -0.3f, 0), 0.24f, 3.3f, 9);
            Blob(fb, 1, rng, new Vector3(0, 3.75f, 0), new Vector3(1.65f, 1.6f, 1.55f));
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 2.39996f;
                var end = new Vector3(Mathf.Cos(angle) * 1.1f, Range(rng, 2.9f, 4.1f), Mathf.Sin(angle) * 1.1f);
                BoxBetween(fb, 0, new Vector3(0, 2, 0), end, 0.12f, 0.14f);
                Blob(fb, 1, rng, end, new Vector3(1.05f, 1.1f, 1.05f));
            }
            return fb.ToMesh(name);
        }

        static Mesh BuildBush(string name, int seed)
        {
            var rng = new System.Random(seed);
            var fb = new FlatMeshBuilder(1);
            Blob(fb, 0, rng, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.7f, 0.9f));
            Blob(fb, 0, rng, new Vector3(0.7f, 0.3f, 0.25f), new Vector3(0.6f, 0.5f, 0.6f));
            Blob(fb, 0, rng, new Vector3(-0.55f, 0.3f, -0.35f), new Vector3(0.55f, 0.45f, 0.55f));
            return fb.ToMesh(name);
        }

        static Mesh BuildFlowers(string name, int seed) => BotanicalFlowerMesh.Build(name, seed);

        static Mesh BuildReeds(string name, int seed)
        {
            var rng = new System.Random(seed);
            var fb = new FlatMeshBuilder(2);
            for (int i = 0; i < 10; i++)
            {
                var basePos = new Vector3(Range(rng, -0.5f, 0.5f), -0.6f, Range(rng, -0.5f, 0.5f));
                float h = Range(rng, 1.6f, 2.3f);
                var apex = basePos + new Vector3(Range(rng, -0.3f, 0.3f), h, Range(rng, -0.3f, 0.3f));
                LeanCone(fb, 0, basePos, 0.05f, apex, 3);

                if (i % 3 == 0)
                {
                    // Bông sậy màu nâu ở gần ngọn
                    var dir = (apex - basePos).normalized;
                    var headCenter = basePos + dir * (h * 0.78f);
                    Box(fb, 1, headCenter, new Vector3(0.09f, 0.32f, 0.09f), Quaternion.FromToRotation(Vector3.up, dir));
                }
            }
            return fb.ToMesh(name);
        }

        static Mesh BuildLilyPad(string name, bool withFlower)
        {
            var fb = new FlatMeshBuilder(withFlower ? 2 : 1);
            const int segments = 10;
            const float radius = 0.55f;
            // Bỏ một múi để lá có khe hình chữ V như lá súng thật
            for (int i = 1; i < segments; i++)
                fb.TriUp(0, Vector3.zero, Ring(radius, i, segments, 0f), Ring(radius, i + 1, segments, 0f));

            if (withFlower)
            {
                Cone(fb, 1, new Vector3(-0.1f, 0f, -0.05f), 0.2f, 0.18f, 6, 0f, false);
                Cone(fb, 1, new Vector3(-0.1f, 0.05f, -0.05f), 0.12f, 0.22f, 5, 30f, false);
            }
            return fb.ToMesh(name);
        }

        static Mesh BuildLog(string name)
        {
            var fb = new FlatMeshBuilder(2);
            const int sides = 8;
            const float r = 0.3f, half = 2f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(0f, Mathf.Cos(a0) * r, Mathf.Sin(a0) * r);
                var p1 = new Vector3(0f, Mathf.Cos(a1) * r, Mathf.Sin(a1) * r);
                var l0 = p0 + Vector3.left * half; var l1 = p1 + Vector3.left * half;
                var r0 = p0 + Vector3.right * half; var r1 = p1 + Vector3.right * half;
                fb.TriOutward(0, l0, r0, r1, Vector3.zero);
                fb.TriOutward(0, l0, r1, l1, Vector3.zero);
                fb.TriOutward(1, Vector3.left * half, l0, l1, Vector3.zero);
                fb.TriOutward(1, Vector3.right * half, r0, r1, Vector3.zero);
            }
            return fb.ToMesh(name);
        }

        // ---------- Mây ----------

        static Mesh BuildCloud(string name, int seed)
        {
            var rng = new System.Random(seed);
            var fb = new FlatMeshBuilder(1);
            Blob(fb, 0, rng, Vector3.zero, new Vector3(3f, 1.5f, 2.2f));
            Blob(fb, 0, rng, new Vector3(2.4f, -0.3f, 0.5f), new Vector3(2f, 1.1f, 1.6f));
            Blob(fb, 0, rng, new Vector3(-2.3f, -0.4f, -0.3f), new Vector3(2.1f, 1f, 1.5f));
            Blob(fb, 0, rng, new Vector3(0.8f, 0.6f, -0.9f), new Vector3(1.6f, 1.1f, 1.4f));
            return fb.ToMesh(name);
        }

        // ---------- Nhà, bến, biển báo, cầu ----------

        static Mesh BuildCabin(string name)
        {
            var fb = new FlatMeshBuilder(4);
            // Tường cắm sâu xuống 1 m để đặt trên dốc không bị hở chân
            Box(fb, 0, new Vector3(0f, 0.8f, 0f), new Vector3(5f, 3.6f, 4f), Quaternion.identity);

            var roofInside = new Vector3(0f, 2.9f, 0f);
            var ridgeL = new Vector3(-2.8f, 4.2f, 0f);
            var ridgeR = new Vector3(2.8f, 4.2f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                var eaveL = new Vector3(-2.8f, 2.4f, 2.4f * side);
                var eaveR = new Vector3(2.8f, 2.4f, 2.4f * side);
                fb.TriOutward(2, eaveL, ridgeL, ridgeR, roofInside);
                fb.TriOutward(2, eaveL, ridgeR, eaveR, roofInside);
                // Mặt dưới mái, để nhìn từ dưới lên không thấy xuyên qua
                fb.TriOutward(1, eaveL, ridgeR, ridgeL, roofInside + Vector3.up * 3f);
                fb.TriOutward(1, eaveL, eaveR, ridgeR, roofInside + Vector3.up * 3f);
            }

            foreach (float x in new[] { -2.5f, 2.5f })
                fb.TriOutward(0, new Vector3(x, 2.6f, -2f), new Vector3(x, 4.05f, 0f), new Vector3(x, 2.6f, 2f), new Vector3(0f, 3f, 0f));

            Box(fb, 1, new Vector3(0.8f, 0.95f, -2.02f), new Vector3(1f, 1.9f, 0.08f), Quaternion.identity);
            Box(fb, 1, new Vector3(-1.3f, 1.5f, -2.02f), new Vector3(0.9f, 0.8f, 0.08f), Quaternion.identity);
            Box(fb, 1, new Vector3(2.52f, 1.5f, 0f), new Vector3(0.08f, 0.8f, 0.9f), Quaternion.identity);
            Box(fb, 3, new Vector3(1.5f, 4f, 0.8f), new Vector3(0.6f, 1.8f, 0.6f), Quaternion.identity);
            AddHouseDetails(fb, 2.6f, 2.0f);
            return fb.ToMesh(name);
        }

        /// <summary>Bến gỗ, trục +z chĩa ra sông. Gốc tọa độ đặt ở mép nước.</summary>
        static Mesh BuildDock(string name)
        {
            var fb = new FlatMeshBuilder(2);
            const float deckY = 0.5f, width = 2.4f;
            for (float z = -3.5f; z < 3f; z += 0.5f)
                Box(fb, 0, new Vector3(0f, deckY, z + 0.22f), new Vector3(width, 0.1f, 0.44f), Quaternion.identity);

            foreach (float z in new[] { -3.2f, 0f, 2.8f })
                foreach (float x in new[] { -1.1f, 1.1f })
                    Box(fb, 1, new Vector3(x, deckY - 1.2f, z), new Vector3(0.2f, 2.8f, 0.2f), Quaternion.identity);
            return fb.ToMesh(name);
        }

        /// <summary>Biển báo khúc cua, mũi tên chỉ theo trục +x, mặt bảng quay về -z.</summary>
        static Mesh BuildSign(string name)
        {
            var fb = new FlatMeshBuilder(3);
            Box(fb, 0, new Vector3(0f, 0.9f, 0f), new Vector3(0.15f, 2.6f, 0.15f), Quaternion.identity);
            Box(fb, 1, new Vector3(0f, 2f, -0.1f), new Vector3(1.6f, 0.8f, 0.08f), Quaternion.identity);

            foreach (float x in new[] { 0.2f, -0.2f })
            {
                Box(fb, 2, new Vector3(x, 2.14f, -0.16f), new Vector3(0.5f, 0.13f, 0.04f), Quaternion.Euler(0f, 0f, -35f));
                Box(fb, 2, new Vector3(x, 1.86f, -0.16f), new Vector3(0.5f, 0.13f, 0.04f), Quaternion.Euler(0f, 0f, 35f));
            }
            return fb.ToMesh(name);
        }

        /// <summary>
        /// Cầu gỗ vồng lên ở giữa, bắc theo trục x. Hai đầu cầu ở độ cao 0, giữa cầu cao <paramref name="rise"/>.
        /// </summary>
        public static Mesh BuildBridge(string name, float span, float rise)
        {
            var fb = new FlatMeshBuilder(2);
            float half = span * 0.5f;
            const float deckWidth = 3f;
            float Y(float x) => rise * (1f - (x / half) * (x / half));
            float Slope(float x) => Mathf.Atan(-2f * rise * x / (half * half)) * Mathf.Rad2Deg;

            for (float x = -half; x < half - 0.2f; x += 0.5f)
            {
                float cx = x + 0.225f;
                Box(fb, 0, new Vector3(cx, Y(cx), 0f), new Vector3(0.46f, 0.12f, deckWidth), Quaternion.Euler(0f, 0f, Slope(cx)));
            }

            const float step = 2.5f;
            int count = Mathf.CeilToInt(span / step);
            for (int i = 0; i <= count; i++)
            {
                float x0 = Mathf.Lerp(-half, half, i / (float)count);
                foreach (float z in new[] { -1.4f, 1.4f })
                {
                    Box(fb, 1, new Vector3(x0, Y(x0) + 0.55f, z), new Vector3(0.15f, 1.1f, 0.15f), Quaternion.identity);
                    if (i == count)
                        continue;

                    float x1 = Mathf.Lerp(-half, half, (i + 1) / (float)count);
                    BoxBetween(fb, 0, new Vector3(x0, Y(x0) + 1.05f, z), new Vector3(x1, Y(x1) + 1.05f, z), 0.1f, 0.1f);
                    BoxBetween(fb, 1, new Vector3(x0, Y(x0) - 0.2f, z * 0.8f), new Vector3(x1, Y(x1) - 0.2f, z * 0.8f), 0.25f, 0.25f);
                }
            }

            // Trụ cầu cắm xuống đất ở hai đầu
            foreach (float x in new[] { -half + 0.5f, half - 0.5f })
                foreach (float z in new[] { -1.2f, 1.2f })
                    Box(fb, 1, new Vector3(x, (Y(x) - 5f) * 0.5f, z), new Vector3(0.35f, Y(x) + 5f, 0.35f), Quaternion.identity);

            return fb.ToMesh(name);
        }

        // ---------- Làng ----------

        static Mesh BuildHouseTall(string name)
        {
            var fb = new FlatMeshBuilder(4);
            var id = Quaternion.identity;
            Box(fb, 0, new Vector3(0f, 1.6f, 0f), new Vector3(5f, 5.2f, 4.5f), id);
            // Dải gỗ tối ngăn hai tầng
            Box(fb, 1, new Vector3(0f, 2.4f, 0f), new Vector3(5.08f, 0.15f, 4.58f), id);

            var roofInside = new Vector3(0f, 4.8f, 0f);
            var ridgeL = new Vector3(-2.9f, 6.2f, 0f);
            var ridgeR = new Vector3(2.9f, 6.2f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                var eaveL = new Vector3(-2.9f, 3.9f, 2.65f * side);
                var eaveR = new Vector3(2.9f, 3.9f, 2.65f * side);
                fb.TriOutward(2, eaveL, ridgeL, ridgeR, roofInside);
                fb.TriOutward(2, eaveL, ridgeR, eaveR, roofInside);
                fb.TriOutward(1, eaveL, ridgeR, ridgeL, roofInside + Vector3.up * 3f);
                fb.TriOutward(1, eaveL, eaveR, ridgeR, roofInside + Vector3.up * 3f);
            }
            foreach (float x in new[] { -2.5f, 2.5f })
                fb.TriOutward(0, new Vector3(x, 4.2f, -2.25f), new Vector3(x, 6.0f, 0f), new Vector3(x, 4.2f, 2.25f), new Vector3(0f, 4.6f, 0f));

            Box(fb, 1, new Vector3(0.9f, 0.95f, -2.27f), new Vector3(1f, 1.9f, 0.08f), id);
            Box(fb, 1, new Vector3(-1.2f, 1.4f, -2.27f), new Vector3(0.9f, 0.8f, 0.08f), id);
            Box(fb, 1, new Vector3(-1.2f, 3.3f, -2.27f), new Vector3(0.8f, 0.7f, 0.08f), id);
            Box(fb, 1, new Vector3(1.0f, 3.3f, -2.27f), new Vector3(0.8f, 0.7f, 0.08f), id);
            Box(fb, 1, new Vector3(2.52f, 3.3f, 0f), new Vector3(0.08f, 0.7f, 0.8f), id);
            Box(fb, 3, new Vector3(-1.6f, 5.8f, 0.9f), new Vector3(0.6f, 2f, 0.6f), id);
            AddHouseDetails(fb, 4.2f, 2.25f);
            return fb.ToMesh(name);
        }

        static Mesh BuildHut(string name)
        {
            var fb = new FlatMeshBuilder(4);
            Prism(fb, 0, new Vector3(0f, -0.8f, 0f), 2f, 3f, 8);
            Cone(fb, 2, new Vector3(0f, 2.1f, 0f), 2.7f, 2.4f, 8, 22.5f, true);
            Box(fb, 1, new Vector3(0f, 0.9f, -1.92f), new Vector3(0.9f, 1.8f, 0.2f), Quaternion.identity);
            return fb.ToMesh(name);
        }

        /// <summary>Một đoạn hàng rào dài 2,5 m theo trục x, cột ở đầu -x.</summary>
        static Mesh BuildFence(string name)
        {
            var fb = new FlatMeshBuilder(1);
            Box(fb, 0, new Vector3(-1.25f, 0.45f, 0f), new Vector3(0.12f, 1.1f, 0.12f), Quaternion.identity);
            BoxBetween(fb, 0, new Vector3(-1.25f, 0.4f, 0f), new Vector3(1.25f, 0.4f, 0f), 0.08f, 0.06f);
            BoxBetween(fb, 0, new Vector3(-1.25f, 0.8f, 0f), new Vector3(1.25f, 0.8f, 0f), 0.08f, 0.06f);
            return fb.ToMesh(name);
        }

        static Mesh BuildWell(string name)
        {
            var fb = new FlatMeshBuilder(4);
            Prism(fb, 3, new Vector3(0f, -0.3f, 0f), 0.9f, 1.1f, 8);
            foreach (float x in new[] { -0.8f, 0.8f })
                Box(fb, 0, new Vector3(x, 1f, 0f), new Vector3(0.12f, 2f, 0.12f), Quaternion.identity);
            Box(fb, 1, new Vector3(0f, 1.7f, 0f), new Vector3(1.6f, 0.1f, 0.1f), Quaternion.identity);

            var inside = new Vector3(0f, 2.1f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                var a = new Vector3(-1.1f, 1.9f, 0.9f * side);
                var b = new Vector3(1.1f, 1.9f, 0.9f * side);
                var ra = new Vector3(-1.1f, 2.5f, 0f);
                var rb = new Vector3(1.1f, 2.5f, 0f);
                fb.TriOutward(2, a, ra, rb, inside);
                fb.TriOutward(2, a, rb, b, inside);
                fb.TriOutward(1, a, rb, ra, inside + Vector3.up * 2f);
                fb.TriOutward(1, a, b, rb, inside + Vector3.up * 2f);
            }
            return fb.ToMesh(name);
        }

        static Mesh BuildHaystack(string name)
        {
            var fb = new FlatMeshBuilder(1);
            Prism(fb, 0, new Vector3(0f, -0.2f, 0f), 1.2f, 0.8f, 8);
            Cone(fb, 0, new Vector3(0f, 0.6f, 0f), 1.3f, 1.5f, 8, 0f, true);
            return fb.ToMesh(name);
        }

        /// <summary>Bánh xe cối xay nước, trục quay theo trục x, trục gỗ chĩa về phía +x để nối vào nhà.</summary>
        static Mesh BuildMillWheel(string name)
        {
            var fb = new FlatMeshBuilder(2);
            const float R = 2.2f;
            Vector3 P(float x, float a, float r) => new(x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);

            foreach (float x in new[] { -0.35f, 0.35f })
            {
                const int rim = 16;
                for (int i = 0; i < rim; i++)
                {
                    float a0 = i / (float)rim * Mathf.PI * 2f, a1 = (i + 1) / (float)rim * Mathf.PI * 2f;
                    BoxBetween(fb, 1, P(x, a0, R), P(x, a1, R), 0.12f, 0.12f);
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                    BoxBetween(fb, 0, new Vector3(x, 0f, 0f), P(x, a, R), 0.1f, 0.1f);
                }
            }

            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Box(fb, 0, P(0f, a, R + 0.1f), new Vector3(0.85f, 0.6f, 0.06f), Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f));
            }

            Box(fb, 1, Vector3.zero, new Vector3(0.9f, 0.5f, 0.5f), Quaternion.identity);
            Box(fb, 1, new Vector3(1.6f, 0f, 0f), new Vector3(3.2f, 0.22f, 0.22f), Quaternion.identity);
            return fb.ToMesh(name);
        }

        // ---------- Thú rừng ----------
        // Thân thú: submesh 0 màu chính, 1 màu sáng, 2 màu tối. Chân: 0 màu chân, 1 móng.
        // Gốc tọa độ ở mặt đất, đầu thú hướng +z. Chân có gốc ở hông và duỗi xuống dưới.

        static void BuildDeer(List<Mesh> list)
        {
            var id = Quaternion.identity;
            var b = new FlatMeshBuilder(3);
            Box(b, 0, new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.42f, 0.95f), id);
            Box(b, 1, new Vector3(0f, 0.76f, 0.02f), new Vector3(0.36f, 0.06f, 0.75f), id);
            Box(b, 0, new Vector3(0f, 1.24f, 0.42f), new Vector3(0.18f, 0.5f, 0.2f), Quaternion.Euler(-28f, 0f, 0f));
            Box(b, 0, new Vector3(0f, 1.5f, 0.6f), new Vector3(0.2f, 0.2f, 0.34f), id);
            Box(b, 2, new Vector3(0f, 1.47f, 0.79f), new Vector3(0.08f, 0.07f, 0.05f), id);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(b, 0, new Vector3(0.12f * s, 1.63f, 0.52f), new Vector3(0.05f, 0.16f, 0.09f), Quaternion.Euler(0f, 0f, -35f * s));
                Box(b, 1, new Vector3(0.07f * s, 1.78f, 0.56f), new Vector3(0.035f, 0.32f, 0.035f), Quaternion.Euler(0f, 0f, -18f * s));
                Box(b, 1, new Vector3(0.14f * s, 1.86f, 0.56f), new Vector3(0.03f, 0.16f, 0.03f), Quaternion.Euler(0f, 0f, -50f * s));
            }
            Box(b, 1, new Vector3(0f, 1.08f, -0.5f), new Vector3(0.09f, 0.14f, 0.06f), Quaternion.Euler(20f, 0f, 0f));
            list.Add(b.ToMesh("Deer_Body"));

            var l = new FlatMeshBuilder(2);
            Box(l, 0, new Vector3(0f, -0.36f, 0f), new Vector3(0.09f, 0.72f, 0.09f), id);
            Box(l, 1, new Vector3(0f, -0.73f, 0f), new Vector3(0.1f, 0.06f, 0.1f), id);
            list.Add(l.ToMesh("Deer_Leg"));
        }

        static void BuildFox(List<Mesh> list)
        {
            var id = Quaternion.identity;
            var b = new FlatMeshBuilder(3);
            Box(b, 0, new Vector3(0f, 0.4f, 0f), new Vector3(0.24f, 0.22f, 0.58f), id);
            Box(b, 1, new Vector3(0f, 0.36f, 0.24f), new Vector3(0.19f, 0.14f, 0.14f), id);
            Box(b, 0, new Vector3(0f, 0.52f, 0.36f), new Vector3(0.2f, 0.18f, 0.2f), id);
            Box(b, 1, new Vector3(0f, 0.48f, 0.51f), new Vector3(0.09f, 0.08f, 0.14f), id);
            Box(b, 2, new Vector3(0f, 0.49f, 0.59f), new Vector3(0.04f, 0.04f, 0.03f), id);
            foreach (float s in new[] { -1f, 1f })
                Box(b, 2, new Vector3(0.065f * s, 0.65f, 0.35f), new Vector3(0.06f, 0.1f, 0.04f), Quaternion.Euler(0f, 0f, -10f * s));
            BoxBetween(b, 0, new Vector3(0f, 0.42f, -0.27f), new Vector3(0f, 0.3f, -0.72f), 0.14f, 0.14f);
            Box(b, 1, new Vector3(0f, 0.28f, -0.78f), new Vector3(0.12f, 0.12f, 0.12f), id);
            list.Add(b.ToMesh("Fox_Body"));

            var l = new FlatMeshBuilder(2);
            Box(l, 0, new Vector3(0f, -0.14f, 0f), new Vector3(0.06f, 0.29f, 0.06f), id);
            Box(l, 1, new Vector3(0f, -0.28f, 0.01f), new Vector3(0.07f, 0.03f, 0.08f), id);
            list.Add(l.ToMesh("Fox_Leg"));
        }

        static void BuildBear(List<Mesh> list)
        {
            var id = Quaternion.identity;
            var b = new FlatMeshBuilder(3);
            Box(b, 0, new Vector3(0f, 0.88f, 0f), new Vector3(0.78f, 0.7f, 1.35f), id);
            Box(b, 0, new Vector3(0f, 1.2f, 0.35f), new Vector3(0.6f, 0.3f, 0.5f), id);
            Box(b, 0, new Vector3(0f, 1.05f, 0.85f), new Vector3(0.48f, 0.44f, 0.44f), id);
            Box(b, 1, new Vector3(0f, 0.98f, 1.1f), new Vector3(0.24f, 0.2f, 0.16f), id);
            Box(b, 2, new Vector3(0f, 1.03f, 1.19f), new Vector3(0.1f, 0.07f, 0.04f), id);
            foreach (float s in new[] { -1f, 1f })
                Box(b, 0, new Vector3(0.19f * s, 1.31f, 0.82f), new Vector3(0.12f, 0.12f, 0.08f), id);
            Box(b, 0, new Vector3(0f, 0.95f, -0.69f), new Vector3(0.12f, 0.12f, 0.08f), id);
            list.Add(b.ToMesh("Bear_Body"));

            var l = new FlatMeshBuilder(2);
            Box(l, 0, new Vector3(0f, -0.27f, 0f), new Vector3(0.24f, 0.56f, 0.26f), id);
            Box(l, 1, new Vector3(0f, -0.54f, 0.03f), new Vector3(0.25f, 0.06f, 0.3f), id);
            list.Add(l.ToMesh("Bear_Leg"));
        }

        static Mesh BuildRabbit(string name)
        {
            var id = Quaternion.identity;
            var b = new FlatMeshBuilder(3);
            Box(b, 0, new Vector3(0f, 0.17f, 0f), new Vector3(0.22f, 0.22f, 0.32f), id);
            Box(b, 0, new Vector3(0f, 0.3f, 0.17f), new Vector3(0.16f, 0.15f, 0.16f), id);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(b, 0, new Vector3(0.04f * s, 0.46f, 0.14f), new Vector3(0.04f, 0.2f, 0.06f), Quaternion.Euler(-10f, 0f, -8f * s));
                Box(b, 1, new Vector3(0.07f * s, 0.03f, 0.08f), new Vector3(0.06f, 0.06f, 0.14f), id);
            }
            Box(b, 1, new Vector3(0f, 0.2f, -0.17f), new Vector3(0.08f, 0.08f, 0.06f), id);
            Box(b, 2, new Vector3(0f, 0.3f, 0.26f), new Vector3(0.03f, 0.03f, 0.02f), id);
            return b.ToMesh(name);
        }

        /// <summary>Vịt: submesh 0 thân, 1 đầu, 2 mỏ. Gốc tọa độ ở mặt nước.</summary>
        static Mesh BuildDuck(string name)
        {
            var b = new FlatMeshBuilder(3);
            Box(b, 0, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.16f, 0.36f), Quaternion.identity);
            Box(b, 0, new Vector3(0f, 0.13f, -0.2f), new Vector3(0.12f, 0.08f, 0.1f), Quaternion.Euler(-25f, 0f, 0f));
            Box(b, 1, new Vector3(0f, 0.2f, 0.12f), new Vector3(0.08f, 0.12f, 0.08f), Quaternion.identity);
            Box(b, 1, new Vector3(0f, 0.28f, 0.14f), new Vector3(0.12f, 0.13f, 0.13f), Quaternion.identity);
            Box(b, 2, new Vector3(0f, 0.27f, 0.24f), new Vector3(0.06f, 0.035f, 0.1f), Quaternion.identity);
            return b.ToMesh(name);
        }

        static void BuildBird(List<Mesh> list)
        {
            var id = Quaternion.identity;
            var b = new FlatMeshBuilder(1);
            Box(b, 0, Vector3.zero, new Vector3(0.12f, 0.1f, 0.32f), id);
            Box(b, 0, new Vector3(0f, 0.04f, 0.18f), new Vector3(0.08f, 0.08f, 0.08f), id);
            Box(b, 0, new Vector3(0f, 0f, -0.2f), new Vector3(0.12f, 0.02f, 0.1f), id);
            list.Add(b.ToMesh("Bird_Body"));

            var w = new FlatMeshBuilder(1);
            Box(w, 0, new Vector3(0.3f, 0f, 0f), new Vector3(0.56f, 0.02f, 0.18f), id);
            list.Add(w.ToMesh("Bird_Wing"));
        }

        static void AddHouseDetails(FlatMeshBuilder fb, float wallTop, float front)
        {
            var id = Quaternion.identity;
            bool tall = wallTop > 3f;
            float ridge = tall ? 6.2f : 4.2f;
            float eave = tall ? 3.9f : 2.4f;
            float roofDepth = tall ? 2.65f : 2.4f;
            float roofAngle = Mathf.Atan2(ridge - eave, roofDepth) * Mathf.Rad2Deg;
            // Individual tiles sit above the existing watertight roof shell.
            foreach (float side in new[] { -1f, 1f })
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 14; col++)
                    {
                        float z = (row + 0.5f) * roofDepth / 7f;
                        float y = Mathf.Lerp(ridge, eave, z / roofDepth) + 0.045f;
                        float x = -2.65f + col * 0.40f + (row % 2) * 0.06f;
                        Box(fb, 2, new Vector3(x, y, z * side), new Vector3(0.38f, 0.065f, roofDepth / 7f / Mathf.Cos(roofAngle * Mathf.Deg2Rad) + 0.035f), Quaternion.Euler(roofAngle * side, 0, 0));
                    }
            Box(fb, 2, new Vector3(0, ridge + 0.09f, 0), new Vector3(5.7f, 0.16f, 0.22f), id);
            foreach (float x in new[] { -2.8f, 2.8f })
                foreach (float side in new[] { -1f, 1f })
                    BoxBetween(fb, 1, new Vector3(x, ridge, 0), new Vector3(x, eave, roofDepth * side), 0.14f, 0.14f);
            // Front dormer breaks up the long roof with a proper little gable.
            float dormerBase = tall ? 4.5f : 2.8f;
            float dormerEave = dormerBase + 0.75f;
            float dormerRidge = dormerBase + 1.4f;
            float dormerFront = -roofDepth + 0.25f;
            Box(fb, 0, new Vector3(0, dormerBase + 0.35f, dormerFront + 0.45f), new Vector3(1.4f, 0.8f, 0.9f), id);
            fb.TriOutward(0, new Vector3(-0.7f, dormerEave, dormerFront), new Vector3(0, dormerRidge, dormerFront), new Vector3(0.7f, dormerEave, dormerFront), new Vector3(0, dormerBase, 0));
            foreach (float side in new[] { -1f, 1f })
            {
                var a = new Vector3(side * 0.88f, dormerEave, dormerFront - 0.15f);
                var b = new Vector3(0, dormerRidge, dormerFront - 0.15f);
                var c = new Vector3(0, dormerRidge, dormerFront + 1.35f);
                var d = new Vector3(side * 0.88f, dormerEave, dormerFront + 1.35f);
                fb.TriOutward(2, a, b, c, new Vector3(0, dormerBase, dormerFront + 0.5f));
                fb.TriOutward(2, a, c, d, new Vector3(0, dormerBase, dormerFront + 0.5f));
                BoxBetween(fb, 1, a, b, 0.085f, 0.085f);
            }
            // Porch rails leave a clear entrance opposite the front door.
            foreach (float x in new[] { -2.3f, -1.1f, 2.3f })
                Box(fb, 1, new Vector3(x, 0.65f, -front - 1.35f), new Vector3(0.1f, 1f, 0.1f), id);
            Box(fb, 1, new Vector3(-1.7f, 1.1f, -front - 1.35f), new Vector3(1.3f, 0.1f, 0.1f), id);
            for (int i = 0; i < 5; i++)
                Box(fb, 0, new Vector3(-2.2f + i * 0.25f, 0.7f, -front - 1.35f), new Vector3(0.05f, 0.7f, 0.05f), id);
            // Raised panels in the entrance door.
            foreach (float y in new[] { 0.5f, 1.3f })
                Box(fb, 0, new Vector3(0.8f, y, -front - 0.075f), new Vector3(0.68f, 0.57f, 0.035f), id);
            // Horizontal siding and braces give the facade scale.
            for (float y = 0.45f; y < wallTop - 0.2f; y += 0.42f)
                Box(fb, 1, new Vector3(0, y, front + 0.025f), new Vector3(4.95f, 0.035f, 0.045f), id);
            foreach (float x in new[] { -2.35f, 2.35f })
                BoxBetween(fb, 1, new Vector3(x, wallTop - 0.2f, -front - 0.07f), new Vector3(x * 0.65f, wallTop - 0.9f, -front - 0.07f), 0.11f, 0.11f);
            if (tall)
            {
                Box(fb, 1, new Vector3(0, 2.65f, -front - 0.65f), new Vector3(4.5f, 0.16f, 1.3f), id);
                Box(fb, 1, new Vector3(0, 3.45f, -front - 1.25f), new Vector3(4.5f, 0.10f, 0.10f), id);
                for (int i = 0; i < 15; i++)
                    Box(fb, 0, new Vector3(-2.1f + i * 0.3f, 3.05f, -front - 1.25f), new Vector3(0.065f, 0.8f, 0.065f), id);
            }
            // Stone plinth, corner timbers, projecting porch and timber balustrade.
            Box(fb, 3, new Vector3(0, -0.12f, 0), new Vector3(5.3f, 0.45f, front * 2 + 0.25f), id);
            foreach (float x in new[] { -2.46f, 2.46f })
                foreach (float z in new[] { -front, front })
                    Box(fb, 1, new Vector3(x, wallTop / 2, z), new Vector3(0.16f, wallTop, 0.16f), id);
            Box(fb, 1, new Vector3(0, wallTop - 0.12f, -front - 0.06f), new Vector3(5.1f, 0.18f, 0.16f), id);
            for (int i = 0; i < 12; i++)
                Box(fb, 1, new Vector3(-2.3f + i * 0.42f, 0.12f, -front - 0.7f), new Vector3(0.39f, 0.14f, 1.5f), id);
            for (int i = 0; i < 3; i++)
                Box(fb, 3, new Vector3(0.8f, -0.15f - i * 0.15f, -front - 1.5f - i * 0.3f), new Vector3(1.5f, 0.2f, 0.5f), id);
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                Box(fb, 1, new Vector3(x, 1.25f, -front - 1.3f), new Vector3(0.13f, 2.4f, 0.13f), id);
                Box(fb, 2, new Vector3(x * 0.5f, 2.45f, -front - 0.75f), new Vector3(2.65f, 0.15f, 1.9f), Quaternion.Euler(-8, 0, 0));
            }
            foreach (float y in wallTop > 3 ? new[] { 1.45f, 3.3f } : new[] { 1.5f })
                foreach (float x in new[] { -1.25f, 1.1f })
                {
                    if (y < 2 && x > 0) continue;
                    Box(fb, 3, new Vector3(x, y, -front - 0.07f), new Vector3(0.78f, 0.65f, 0.08f), id);
                    foreach (float dx in new[] { -0.48f, 0.48f })
                        Box(fb, 2, new Vector3(x + dx, y, -front - 0.12f), new Vector3(0.22f, 0.86f, 0.10f), id);
                    Box(fb, 0, new Vector3(x, y, -front - 0.14f), new Vector3(0.06f, 0.7f, 0.05f), id);
                    Box(fb, 0, new Vector3(x, y, -front - 0.14f), new Vector3(0.8f, 0.06f, 0.05f), id);
                    Box(fb, 1, new Vector3(x, y - 0.5f, -front - 0.25f), new Vector3(1.1f, 0.22f, 0.4f), id);
                }
        }

        // ---------- Khối hình cơ bản ----------

        static void Box(FlatMeshBuilder fb, int submesh, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(int x, int y, int z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                fb.TriOutward(submesh, a, b, c, center);
                fb.TriOutward(submesh, a, c, d, center);
            }

            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
        }

        static void BoxBetween(FlatMeshBuilder fb, int submesh, Vector3 a, Vector3 b, float thickness, float width)
        {
            Vector3 dir = b - a;
            Box(fb, submesh, (a + b) * 0.5f, new Vector3(width, thickness, dir.magnitude), Quaternion.LookRotation(dir, Vector3.up));
        }

        static void Prism(FlatMeshBuilder fb, int submesh, Vector3 baseCenter, float radius, float height, int sides)
        {
            Vector3 inside = baseCenter + Vector3.up * (height * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a0 = baseCenter + Ring(radius, i, sides, 0f);
                Vector3 b0 = baseCenter + Ring(radius, i + 1, sides, 0f);
                Vector3 a1 = a0 + Vector3.up * height, b1 = b0 + Vector3.up * height;
                fb.TriOutward(submesh, a0, a1, b1, inside);
                fb.TriOutward(submesh, a0, b1, b0, inside);
                fb.TriOutward(submesh, baseCenter, b0, a0, inside);
                fb.TriOutward(submesh, baseCenter + Vector3.up * height, a1, b1, inside);
            }
        }

        static void Cone(FlatMeshBuilder fb, int submesh, Vector3 baseCenter, float radius, float height, int sides, float angleOffset, bool bottomCap)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            Vector3 inside = baseCenter + Vector3.up * (height * 0.3f);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = baseCenter + Ring(radius, i, sides, angleOffset);
                Vector3 b = baseCenter + Ring(radius, i + 1, sides, angleOffset);
                fb.TriOutward(submesh, a, apex, b, inside);
                if (bottomCap)
                    fb.TriOutward(submesh, a, b, baseCenter, inside);
            }
        }

        static void LeanCone(FlatMeshBuilder fb, int submesh, Vector3 baseCenter, float radius, Vector3 apex, int sides)
        {
            Vector3 inside = Vector3.Lerp(baseCenter, apex, 0.3f);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = baseCenter + Ring(radius, i, sides, 0f);
                Vector3 b = baseCenter + Ring(radius, i + 1, sides, 0f);
                fb.TriOutward(submesh, a, apex, b, inside);
            }
        }

        static void Blob(FlatMeshBuilder fb, int submesh, System.Random rng, Vector3 center, Vector3 size)
        {
            Icosphere(1, out var unit, out var tris);
            var v = new Vector3[unit.Count];
            for (int i = 0; i < v.Length; i++)
            {
                float r = 0.85f + (float)rng.NextDouble() * 0.25f;
                v[i] = center + Vector3.Scale(unit[i] * r, size);
            }

            for (int i = 0; i < tris.Count; i += 3)
                fb.TriOutward(submesh, v[tris[i]], v[tris[i + 1]], v[tris[i + 2]], center);
        }

        static Vector3 Ring(float radius, int i, int sides, float angleOffsetDeg)
        {
            float a = (i / (float)sides) * Mathf.PI * 2f + angleOffsetDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Khối cầu 20 mặt, chia nhỏ <paramref name="subdivisions"/> lần, bán kính 1.</summary>
        static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++)
                verts[i] = verts[i].normalized;

            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var list = verts;
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx))
                        return idx;
                    list.Add(((list[a] + list[b]) * 0.5f).normalized);
                    idx = list.Count - 1;
                    cache[key] = idx;
                    return idx;
                }

                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
        }
    }
}
