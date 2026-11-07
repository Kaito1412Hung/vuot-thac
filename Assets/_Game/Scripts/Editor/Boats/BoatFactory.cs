using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    /// <summary>
    /// Tạo 10 mẫu thuyền: hình dựng bằng code, prefab có vật lý, file thông tin thuyền và danh sách thuyền.
    /// Chạy lại thì cập nhật đè lên, giữ nguyên các liên kết đang dùng.
    /// </summary>
    public static partial class BoatFactory
    {
        const string MaterialFolder = "Assets/_Game/Materials/Boats";
        const string PrefabFolder = "Assets/_Game/Prefabs/Boats";
        const string DataFolder = "Assets/_Game/Data/Boats";
        const string MeshAsset = "Assets/_Game/Generated/Boats.asset";
        public const string CatalogPath = "Assets/_Game/Data/BoatCatalog.asset";

        class Spec
        {
            public string id, name, description;
            public int speed, accel, handling, toughness;
            public float length, beam, depth, bounce = 0.15f;
            public Action<BoatMesh> build;
        }

        // ---------- Vật liệu ----------

        static readonly Dictionary<string, Material> cache = new();

        static Material M(string name, Color color, float smoothness = 0.15f)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null)
                return cached;
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, path);
            }
            if(name.Contains("Wood")||name.Contains("Bamboo")||name.Contains("Bark"))
            {
                var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Wood.mat");
                if(wood!=null&&wood.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",wood.GetTexture("_BaseMap"));
                EditorUtility.SetDirty(mat);
            }
            if(name=="BoatGlass")
            {
                mat.SetColor("_BaseColor",new Color(0.2f,0.34f,0.44f,0.24f));
                mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;EditorUtility.SetDirty(mat);
            }
            cache[name] = mat;
            return mat;
        }

        static Material Wood => M("BoatWood", new Color(0.6f, 0.42f, 0.25f));
        static Material WoodLight => M("BoatWoodLight", new Color(0.8f, 0.64f, 0.43f));
        static Material WoodDark => M("BoatWoodDark", new Color(0.34f, 0.22f, 0.13f));
        static Material Bark => M("BoatBark", new Color(0.28f, 0.19f, 0.12f));
        static Material Bamboo => M("BoatBamboo", new Color(0.8f, 0.72f, 0.4f));
        static Material BambooDark => M("BoatBambooDark", new Color(0.46f, 0.38f, 0.18f));
        static Material Red => M("BoatRed", new Color(0.8f, 0.16f, 0.12f), 0.35f);
        static Material Gold => M("BoatGold", new Color(0.96f, 0.76f, 0.2f), 0.5f);
        static Material Yellow => M("BoatYellow", new Color(0.98f, 0.8f, 0.15f), 0.4f);
        static Material Orange => M("BoatOrange", new Color(0.95f, 0.45f, 0.1f), 0.3f);
        static Material White => M("BoatWhite", new Color(0.95f, 0.95f, 0.93f), 0.45f);
        static Material Blue => M("BoatBlue", new Color(0.15f, 0.38f, 0.72f), 0.4f);
        static Material Gray => M("BoatGray", new Color(0.45f, 0.47f, 0.5f));
        static Material Dark => M("BoatDark", new Color(0.12f, 0.12f, 0.14f), 0.3f);
        static Material Glass => M("BoatGlass", new Color(0.2f, 0.34f, 0.44f), 0.9f);
        static Material Sail => M("BoatSail", new Color(0.97f, 0.95f, 0.88f));
        static Material Skin => M("BoatSkin", new Color(0.87f, 0.68f, 0.52f));
        static Material Hat => M("BoatHat", new Color(0.88f, 0.79f, 0.5f));
        static Material Pants => M("BoatPants", new Color(0.18f, 0.2f, 0.26f));
        static Material Shirt(string name, Color c) => M("BoatShirt" + name, c);

        // ---------- Người chèo ----------

        enum Paddle { None, Single, Double, Pole, Oar }

        /// <summary>
        /// Người chèo đội nón lá. <paramref name="pos"/> là chỗ ngồi (hông), nhìn về +z.
        /// Đứng thì người cao hơn và cầm sào hoặc mái chèo dài.
        /// </summary>
        class RowerSeat
        {
            public Vector3 position;public Material shirt;public Paddle paddle;public float side;public bool standing;
        }
        static readonly List<RowerSeat> rowerSeats=new();
        static void Rower(BoatMesh m,Vector3 pos,Material shirt,Paddle paddle,float side=1,bool standing=false,bool hat=true)
        {
            rowerSeats.Add(new RowerSeat{position=pos,shirt=shirt,paddle=paddle,side=side,standing=standing});
        }

        // ---------- 10 mẫu thuyền ----------

        static List<Spec> Specs() => new()
        {
            new Spec
            {
                id = "XuongBaLa", name = "Xuồng Ba Lá", speed = 6, accel = 6, handling = 6, toughness = 6,
                description = "Chiếc xuồng gỗ quen thuộc của miền sông nước. Mọi thứ đều vừa phải, dễ làm quen nhất.",
                length = 3.6f, beam = 1f, depth = 0.32f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 3.6f, beam = 1f, depth = 0.32f, freeboard = 0.18f, bowFullness = 1.8f, sternFullness = 1.8f, rocker = 0.14f, deckFrom = 2f }, Wood, WoodLight, WoodDark);
                    m.Box(WoodDark, new Vector3(0f, 0.04f, 0.65f), new Vector3(0.8f, 0.04f, 0.18f));
                    m.Box(WoodDark, new Vector3(0f, 0.04f, -0.6f), new Vector3(0.82f, 0.04f, 0.18f));
                    Rower(m, new Vector3(0f, -0.12f, -0.55f), Shirt("Blue", new Color(0.2f, 0.4f, 0.75f)), Paddle.Single, 1f);
                },
            },
            new Spec
            {
                id = "ThuyenThung", name = "Thuyền Thúng", speed = 3, accel = 7, handling = 10, toughness = 4,
                description = "Thúng chai tròn xoay một vòng chỉ trong chớp mắt. Chậm nhưng luồn lách qua ghềnh đá cực giỏi.",
                length = 1.9f, beam = 1.9f, depth = 0.42f,
                build = m =>
                {
                    m.Bowl(0.95f, 0.42f, 0.12f, Bamboo, BambooDark, WoodDark);
                    var ring = new List<Vector3>();
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2f;
                        ring.Add(new Vector3(Mathf.Cos(a) * 0.97f, 0.12f, Mathf.Sin(a) * 0.97f));
                    }
                    m.Tube(WoodDark, ring, 0.04f, 5);
                    Rower(m, new Vector3(0f, -0.25f, -0.2f), Shirt("White", new Color(0.92f, 0.92f, 0.9f)), Paddle.Single, 1f);
                },
            },
            new Spec
            {
                id = "Kayak", name = "Kayak", speed = 7, accel = 8, handling = 7, toughness = 2,
                description = "Nhẹ và lướt nhanh, mái chèo đôi tăng tốc rất gắt. Đổi lại vỏ mỏng, đâm đá là mất đà nhiều.",
                length = 4.2f, beam = 0.62f, depth = 0.22f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 4.2f, beam = 0.62f, depth = 0.22f, freeboard = 0.12f, bowFullness = 1.6f, sternFullness = 1.6f, rocker = 0.1f, deckFrom = -2f }, Yellow, Dark, Dark, Yellow);
                    var ring = new List<Vector3>();
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        ring.Add(new Vector3(Mathf.Cos(a) * 0.27f, 0.16f, -0.2f + Mathf.Sin(a) * 0.42f));
                    }
                    m.Tube(Dark, ring, 0.035f, 5);
                    Rower(m, new Vector3(0f, -0.05f, -0.25f), Shirt("Red", new Color(0.82f, 0.2f, 0.18f)), Paddle.Double, 1f, false, false);
                },
            },
            new Spec
            {
                id = "DocMoc", name = "Thuyền Độc Mộc", speed = 6, accel = 4, handling = 5, toughness = 9,
                description = "Đục từ nguyên một thân cây lớn nên rất nặng và chắc. Va đá gần như không hề hấn gì.",
                length = 4.4f, beam = 0.8f, depth = 0.34f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 4.4f, beam = 0.8f, depth = 0.34f, freeboard = 0.16f, bowFullness = 3.5f, sternFullness = 3.5f, rocker = 0.05f, deckFrom = 2f }, Bark, WoodLight, WoodDark);
                    Rower(m, new Vector3(0f, -0.12f, 0.75f), Shirt("Green", new Color(0.25f, 0.55f, 0.3f)), Paddle.Single, -1f);
                    Rower(m, new Vector3(0f, -0.12f, -0.85f), Shirt("Brown", new Color(0.55f, 0.36f, 0.2f)), Paddle.Single, 1f);
                },
            },
            new Spec
            {
                id = "BeTre", name = "Bè Tre", speed = 4, accel = 5, handling = 5, toughness = 10, bounce = 0.35f,
                description = "Bè kết từ những cây tre lớn, người lái đứng chống sào. Chậm nhưng không thể lật, đâm đâu cũng nảy ra.",
                length = 4f, beam = 2.2f, depth = 0.25f,
                build = m =>
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float x = -0.98f + i * 0.28f;
                        float wobble = Mathf.Sin(i * 1.7f) * 0.1f;
                        m.Cylinder(i % 3 == 0 ? BambooDark : Bamboo, new Vector3(x, 0f, -2f + wobble), new Vector3(x, 0.02f, 2f + wobble), 0.12f, 7);
                    }
                    foreach (float z in new[] { -1.5f, 0f, 1.5f })
                        m.Box(WoodDark, new Vector3(0f, 0.14f, z), new Vector3(2.3f, 0.07f, 0.12f));
                    m.Box(Bamboo, new Vector3(0.5f, 0.3f, 0.9f), new Vector3(0.6f, 0.3f, 0.5f));
                    Rower(m, new Vector3(-0.3f, 0.12f, -1.1f), Shirt("Gray", new Color(0.5f, 0.52f, 0.55f)), Paddle.Pole, 1f, true);
                },
            },
            new Spec
            {
                id = "PhaoCaoSu", name = "Phao Cao Su", speed = 5, accel = 6, handling = 5, toughness = 8, bounce = 0.55f,
                description = "Xuồng phao bơm hơi chuyên đi ghềnh. Đâm vào đá là bật nảy ra, mất rất ít tốc độ.",
                length = 3.7f, beam = 1.7f, depth = 0.3f,
                build = m =>
                {
                    var path = new List<Vector3>();
                    const float straight = 1.1f, radius = 0.72f;
                    for (int i = 0; i <= 8; i++)
                    {
                        float a = Mathf.PI * i / 8f;
                        path.Add(new Vector3(Mathf.Cos(a) * radius, 0.05f, straight + Mathf.Sin(a) * radius));
                    }
                    for (int i = 0; i <= 8; i++)
                    {
                        float a = Mathf.PI + Mathf.PI * i / 8f;
                        path.Add(new Vector3(Mathf.Cos(a) * radius, 0.05f, -straight + Mathf.Sin(a) * radius));
                    }
                    m.Tube(Orange, path, 0.25f, 8);
                    m.Box(Gray, new Vector3(0f, -0.15f, 0f), new Vector3(1.3f, 0.08f, 3f));
                    m.Box(Orange, new Vector3(0f, 0.02f, 0.2f), new Vector3(1.3f, 0.16f, 0.22f));
                    Rower(m, new Vector3(-0.35f, -0.08f, 0.6f), Shirt("Teal", new Color(0.1f, 0.6f, 0.6f)), Paddle.Single, -1f);
                    Rower(m, new Vector3(0.35f, -0.08f, -0.6f), Shirt("Yellow", new Color(0.95f, 0.8f, 0.2f)), Paddle.Single, 1f);
                },
            },
            new Spec
            {
                id = "CaNoMay", name = "Ca Nô Máy", speed = 9, accel = 7, handling = 4, toughness = 4,
                description = "Có gắn máy nổ ở đuôi nên chạy rất nhanh trên đoạn thẳng. Cua khá nặng tay, phải tính trước.",
                length = 4.6f, beam = 1.7f, depth = 0.45f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 4.6f, beam = 1.7f, depth = 0.45f, freeboard = 0.35f, bowFullness = 2.4f, sternFullness = 6f, rocker = 0.15f, sternCut = 0.18f, deckFrom = 0.25f }, White, Gray, Blue, White);
                    m.Box(Glass, new Vector3(0f, 0.62f, 0.5f), new Vector3(1.2f, 0.4f, 0.05f), Quaternion.Euler(-28f, 0f, 0f));
                    m.Box(Gray, new Vector3(0f, 0.1f, -0.5f), new Vector3(0.6f, 0.2f, 0.5f));
                    m.Box(Dark, new Vector3(0f, 0.45f, -2.05f), new Vector3(0.35f, 0.55f, 0.35f));
                    m.Cylinder(Dark, new Vector3(0f, 0.2f, -2.1f), new Vector3(0f, -0.55f, -2.15f), 0.06f, 6);
                    Rower(m, new Vector3(0f, 0.1f, -0.5f), Shirt("Navy", new Color(0.15f, 0.2f, 0.4f)), Paddle.None, 1f, false, false);
                },
            },
            new Spec
            {
                id = "ThuyenRong", name = "Thuyền Rồng", speed = 10, accel = 5, handling = 3, toughness = 6,
                description = "Thuyền đua lễ hội dài nhất, bốn tay chèo cùng nhịp. Nhanh nhất khi đã lấy đà nhưng rất khó quay đầu.",
                length = 6.6f, beam = 0.95f, depth = 0.32f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 6.6f, beam = 0.95f, depth = 0.32f, freeboard = 0.2f, bowFullness = 1.7f, sternFullness = 1.7f, rocker = 0.18f, deckFrom = 2f }, Red, WoodLight, Gold);
                    // Đầu rồng ở mũi
                    m.Cylinder(Red, new Vector3(0f, 0.35f, 3.05f), new Vector3(0f, 1.0f, 3.4f), 0.12f, 6);
                    m.Box(Gold, new Vector3(0f, 1.1f, 3.55f), new Vector3(0.3f, 0.28f, 0.48f));
                    m.Box(Red, new Vector3(0f, 0.98f, 3.72f), new Vector3(0.24f, 0.1f, 0.4f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        m.Cone(Gold, new Vector3(0.1f * s, 1.22f, 3.45f), new Vector3(0.22f * s, 1.55f, 3.25f), 0.05f, 5);
                        m.Box(White, new Vector3(0.15f * s, 1.17f, 3.68f), new Vector3(0.04f, 0.07f, 0.07f));
                    }
                    // Đuôi rồng
                    m.Cone(Gold, new Vector3(0f, 0.35f, -3.15f), new Vector3(0f, 1.05f, -3.6f), 0.14f, 6);
                    var shirt = Shirt("Festival", new Color(0.95f, 0.85f, 0.2f));
                    for (int i = 0; i < 4; i++)
                        Rower(m, new Vector3(0f, -0.12f, 1.6f - i * 1.15f), shirt, Paddle.Single, i % 2 == 0 ? 1f : -1f, false, false);
                    m.Cylinder(Red, new Vector3(0f, 0.05f, 2.4f), new Vector3(0f, 0.4f, 2.4f), 0.2f, 8);
                },
            },
            new Spec
            {
                id = "ThuyenBuom", name = "Thuyền Buồm", speed = 9, accel = 3, handling = 6, toughness = 6,
                description = "Cánh buồm căng gió cho tốc độ rất cao, nhưng lấy đà chậm. Đừng để bị đâm dừng lại giữa chừng.",
                length = 4f, beam = 1.25f, depth = 0.4f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 4f, beam = 1.25f, depth = 0.4f, freeboard = 0.25f, bowFullness = 2f, sternFullness = 3f, rocker = 0.15f, deckFrom = 0.35f }, Blue, White, White, White);
                    m.Cylinder(WoodDark, new Vector3(0f, 0.1f, 0.6f), new Vector3(0f, 4f, 0.6f), 0.06f, 6);
                    m.Cylinder(WoodDark, new Vector3(0f, 0.95f, 0.6f), new Vector3(0f, 0.95f, -1.45f), 0.045f, 6);
                    m.DoubleTri(Sail, new Vector3(0f, 1.05f, 0.55f), new Vector3(0f, 3.9f, 0.57f), new Vector3(0f, 1.05f, -1.4f));
                    m.DoubleTri(Red, new Vector3(0.01f, 1.6f, 0.0f), new Vector3(0.01f, 2.3f, 0.3f), new Vector3(0.01f, 1.6f, -0.6f));
                    Rower(m, new Vector3(0f, -0.1f, -1.3f), Shirt("Striped", new Color(0.9f, 0.9f, 0.95f)), Paddle.None, 1f, false, false);
                    m.Beam(WoodDark, new Vector3(0f, 0.2f, -1.15f), new Vector3(0f, 0.2f, -1.95f), 0.06f, 0.06f);
                },
            },
            new Spec
            {
                id = "TamBan", name = "Thuyền Tam Bản", speed = 6, accel = 5, handling = 6, toughness = 7,
                description = "Thuyền chở hàng có mái che bằng tre, người lái đứng chèo ở đuôi. Chắc chắn và dễ điều khiển.",
                length = 4.6f, beam = 1.4f, depth = 0.4f,
                build = m =>
                {
                    m.Hull(new BoatMesh.HullShape { length = 4.6f, beam = 1.4f, depth = 0.4f, freeboard = 0.28f, bowFullness = 4.5f, sternFullness = 4.5f, rocker = 0.3f, deckFrom = 2f }, WoodDark, Wood, Red);
                    // Mái che hình vòm
                    const int arcs = 8;
                    for (int i = 0; i < arcs; i++)
                    {
                        float a0 = Mathf.PI * i / arcs, a1 = Mathf.PI * (i + 1) / arcs;
                        Vector3 p0 = new(Mathf.Cos(a0) * 0.7f, 0.3f + Mathf.Sin(a0) * 0.75f, 0f);
                        Vector3 p1 = new(Mathf.Cos(a1) * 0.7f, 0.3f + Mathf.Sin(a1) * 0.75f, 0f);
                        m.DoubleQuad(Bamboo, p0 + Vector3.forward * 0.75f, p1 + Vector3.forward * 0.75f, p1 - Vector3.forward * 0.9f, p0 - Vector3.forward * 0.9f);
                        foreach (float z in new[] { 0.75f, -0.08f, -0.9f })
                            m.Beam(BambooDark, p0 + Vector3.forward * z, p1 + Vector3.forward * z, 0.05f, 0.05f);
                    }
                    m.Box(Wood, new Vector3(0f, 0.05f, 1.5f), new Vector3(0.5f, 0.3f, 0.4f));
                    Rower(m, new Vector3(0.1f, -0.05f, -1.75f), Shirt("Indigo", new Color(0.25f, 0.25f, 0.5f)), Paddle.Oar, 1f, true);
                },
            },
        };

        // ---------- Tạo tài nguyên ----------

        [MenuItem("Vượt Thác/Tạo 10 thuyền")]
        public static void CreateAll()
        {
            foreach (var folder in new[] { MaterialFolder, PrefabFolder, DataFolder })
                Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.GetDirectoryName(MeshAsset));
            cache.Clear();
            closedBodyMesh=null;

            if (AssetDatabase.LoadMainAssetAtPath(MeshAsset) != null)
                AssetDatabase.DeleteAsset(MeshAsset);
            // Asset chính bị Unity đổi tên theo tên file, nên dùng một mesh rỗng làm asset chính
            AssetDatabase.CreateAsset(new Mesh { name = "Boats" }, MeshAsset);

            var specs = Specs();
            var defs = new List<BoatDefinition>();
            foreach (var spec in specs)
                defs.Add(CreateBoat(spec));

            var catalog = AssetDatabase.LoadAssetAtPath<BoatCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BoatCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.boats = defs.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Vượt Thác] Đã tạo {defs.Count} thuyền và danh sách thuyền tại {CatalogPath}.");
        }

        static BoatDefinition CreateBoat(Spec spec)
        {
            string defPath = $"{DataFolder}/{spec.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BoatDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BoatDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
                def.displayName = spec.name;
                def.description = spec.description;
                def.speed = spec.speed;
                def.acceleration = spec.accel;
                def.handling = spec.handling;
                def.toughness = spec.toughness;
            }

            var builder = new BoatMesh();
            rowerSeats.Clear();
            spec.build(builder);
            var mesh = builder.Build("Boat_" + spec.id, out var materials);
            AssetDatabase.AddObjectToAsset(mesh, MeshAsset);

            var physics = new PhysicsMaterial("BoatPhysics_" + spec.id)
            {
                bounciness = spec.bounce,
                dynamicFriction = 0.2f,
                staticFriction = 0.2f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.AddObjectToAsset(physics, MeshAsset);

            var root = new GameObject(spec.id);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = def.Mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.5f;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -spec.depth * 0.2f, 0f);
            col.size = new Vector3(spec.beam * 0.95f, spec.depth + 0.25f, spec.length * 0.95f);
            col.sharedMaterial = physics;

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;

            var ctrl = root.AddComponent<BoatController>();
            ctrl.definition = def;
            ctrl.hullLength = spec.length;
            ctrl.hullBeam = spec.beam;
            ctrl.hullDepth = spec.depth;
            ctrl.model = model.transform;

            BuildRealisticCrew(root,model.transform,spec);
            AddBoatFittings(model.transform,spec);
            BuildWaterOcclusion(model.transform,spec,builder.LastHull);

            string prefabPath = $"{PrefabFolder}/{spec.id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            def.prefab = prefab;
            EditorUtility.SetDirty(def);
            return def;
        }

        // ---------- Gắn vào scene ----------

        [MenuItem("Vượt Thác/Thêm màn chọn thuyền vào scene đang mở")]
        public static void AddSelectScreenToScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoatCatalog>(CatalogPath);
            if (catalog == null)
            {
                CreateAll();
                catalog = AssetDatabase.LoadAssetAtPath<BoatCatalog>(CatalogPath);
            }

            var screen = UnityEngine.Object.FindAnyObjectByType<BoatSelectScreen>();
            if (screen == null)
                screen = new GameObject("RaceSession").AddComponent<BoatSelectScreen>();
            screen.catalog = catalog;
            screen.track = UnityEngine.Object.FindAnyObjectByType<RiverTrack>();

            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<BoatCamera>();
                if (follow == null)
                    follow = cam.gameObject.AddComponent<BoatCamera>();
                screen.boatCamera = follow;
            }

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            Debug.Log("[Vượt Thác] Đã gắn màn chọn thuyền vào scene. Bấm Play để chọn thuyền và lái thử.");
        }
    }
}
