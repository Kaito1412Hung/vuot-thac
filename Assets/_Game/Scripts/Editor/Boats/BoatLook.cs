using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    /// <summary>
    /// Phần "trông như thật" của thuyền: chất liệu có texture và normal map, chi tiết đóng thuyền
    /// (mép mạn, sườn, dây buộc, mắt thuyền…) và hiệu ứng nước gắn vào mỗi thuyền.
    /// </summary>
    public static partial class BoatFactory
    {
        // ---------- Vật liệu thêm ----------

        static Material Weave => M("BoatWeave", new Color(0.86f, 0.74f, 0.5f));
        static Material WeaveTar => M("BoatWeaveTar", new Color(0.2f, 0.15f, 0.11f), 0.55f);
        static Material Thatch => M("BoatThatch", new Color(0.82f, 0.7f, 0.45f));
        static Material DragonScales => M("BoatDragonScales", Color.white, 0.4f);
        static Material RubberGray => M("BoatRubberGray", new Color(0.32f, 0.34f, 0.36f), 0.3f);
        static Material Rope => M("BoatRope", new Color(0.72f, 0.62f, 0.42f), 0.05f);
        static Material Steel => M("BoatSteel", new Color(0.78f, 0.8f, 0.82f), 0.75f);
        static Material Black => M("BoatBlack", new Color(0.03f, 0.03f, 0.03f), 0.6f);
        static Material Paper => M("BoatLanternPaper", new Color(0.95f, 0.35f, 0.12f), 0.1f);

        struct Look { public string tex; public Vector2 tiling; public float smooth, metal, bump; public Color? color; }

        /// <summary>Texture theo tên vật liệu. Tiling tính theo mét: UV của thuyền là mét.</summary>
        static readonly Dictionary<string, Look> Looks = new()
        {
            ["BoatWood"] = new Look { tex = "Planks", tiling = new Vector2(0.5f, 1.33f), smooth = 0.18f, bump = 0.9f },
            ["BoatWoodLight"] = new Look { tex = "Planks", tiling = new Vector2(0.5f, 1.33f), smooth = 0.2f, bump = 0.8f },
            ["BoatWoodDark"] = new Look { tex = "Planks", tiling = new Vector2(0.5f, 1.33f), smooth = 0.22f, bump = 0.8f },
            ["BoatBark"] = new Look { tex = "Adzed", tiling = new Vector2(0.55f, 1.1f), smooth = 0.12f, bump = 1.1f, color = new Color(0.42f, 0.29f, 0.18f) },
            ["BoatBamboo"] = new Look { tex = "Bamboo", tiling = new Vector2(0.7f, 2.5f), smooth = 0.4f, bump = 0.9f },
            ["BoatBambooDark"] = new Look { tex = "Bamboo", tiling = new Vector2(0.7f, 2.5f), smooth = 0.35f, bump = 0.9f },
            ["BoatWeave"] = new Look { tex = "Weave", tiling = new Vector2(1.6f, 1.6f), smooth = 0.15f, bump = 1f },
            ["BoatWeaveTar"] = new Look { tex = "Weave", tiling = new Vector2(1.6f, 1.6f), smooth = 0.55f, bump = 0.7f },
            ["BoatThatch"] = new Look { tex = "WeaveFine", tiling = new Vector2(1.4f, 1.4f), smooth = 0.1f, bump = 1f },
            ["BoatDragonScales"] = new Look { tex = "DragonScales", tiling = new Vector2(0.55f, 1.1f), smooth = 0.45f, bump = 0.8f, color = new Color(1f, 0.7f, 0.64f) },
            ["BoatRed"] = new Look { tex = "PaintedPlanks", tiling = new Vector2(0.5f, 1.33f), smooth = 0.35f, bump = 0.6f },
            ["BoatBlue"] = new Look { tex = "Gelcoat", tiling = new Vector2(0.5f, 0.5f), smooth = 0.65f, bump = 0.5f },
            ["BoatWhite"] = new Look { tex = "Gelcoat", tiling = new Vector2(0.5f, 0.5f), smooth = 0.65f, bump = 0.5f },
            ["BoatYellow"] = new Look { tex = "Gelcoat", tiling = new Vector2(0.5f, 0.5f), smooth = 0.7f, bump = 0.5f },
            ["BoatGray"] = new Look { tex = "Gelcoat", tiling = new Vector2(0.5f, 0.5f), smooth = 0.45f, bump = 0.5f },
            ["BoatDark"] = new Look { tex = "Gelcoat", tiling = new Vector2(0.5f, 0.5f), smooth = 0.45f, bump = 0.5f },
            ["BoatOrange"] = new Look { tex = "Rubber", tiling = new Vector2(1f, 1f), smooth = 0.3f, bump = 0.8f },
            ["BoatRubberGray"] = new Look { tex = "Rubber", tiling = new Vector2(1f, 1f), smooth = 0.25f, bump = 0.8f },
            ["BoatSail"] = new Look { tex = "Sailcloth", tiling = new Vector2(0.5f, 0.5f), smooth = 0.1f, bump = 0.6f },
            ["BoatRope"] = new Look { tex = "Bamboo", tiling = new Vector2(8f, 8f), smooth = 0.05f, bump = 1.2f },
            ["BoatGold"] = new Look { smooth = 0.65f, metal = 0.85f },
            ["BoatSteel"] = new Look { smooth = 0.8f, metal = 0.9f },
        };

        static void Dress(Material mat, string name)
        {
            if (!Looks.TryGetValue(name, out var look)) return;
            if (!string.IsNullOrEmpty(look.tex))
            {
                var albedo = BoatTextures.Albedo(look.tex); var normal = BoatTextures.Normal(look.tex);
                if (albedo != null) { mat.SetTexture("_BaseMap", albedo); mat.SetTextureScale("_BaseMap", look.tiling); }
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", look.bump);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            if (look.color.HasValue) mat.SetColor("_BaseColor", look.color.Value);
            mat.SetFloat("_Smoothness", look.smooth);
            mat.SetFloat("_Metallic", look.metal);
            EditorUtility.SetDirty(mat);
        }

        // ---------- Hiệu ứng nước ----------

        static Material FxMaterial(string name, float mode, float alpha)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("VuotThac/WaterFx")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetFloat("_Mode", mode); mat.SetFloat("_Alpha", alpha);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void AddWaterEffects(GameObject root)
        {
            var fx = root.AddComponent<BoatWaterEffects>();
            fx.dot = FxMaterial("WaterFxDot", 0, 1f);
            fx.ring = FxMaterial("WaterFxRing", 1, 1f);
            fx.band = FxMaterial("WaterFxBand", 2, 0.9f);
        }

        // ---------- Hình dạng thân để đặt chi tiết ----------

        struct Station { public float top, bottom, width, z; }

        static Station At(BoatMesh.HullShape h, float t)
        {
            float full = t >= 0 ? h.bowFullness : h.sternFullness;
            return new Station
            {
                width = h.beam * 0.5f * Mathf.Max(0, 1 - Mathf.Pow(Mathf.Abs(t), full)),
                top = h.freeboard + h.rocker * t * t,
                bottom = -h.depth * (1 - 0.4f * t * t) + h.rocker * 0.5f * t * t,
                z = t * h.length * 0.5f,
            };
        }

        /// <summary>Điểm trên mặt ngoài thân: theta = 0 là đáy giữa, ±π/2 là mép mạn.</summary>
        static Vector3 Outer(BoatMesh.HullShape h, float t, float theta, float scale = 1f)
        {
            var s = At(h, t);
            return new Vector3(Mathf.Sin(theta) * s.width * scale, Mathf.Lerp(s.top, s.bottom, Mathf.Cos(theta)), s.z);
        }

        static List<Vector3> Sheer(BoatMesh.HullShape h, float side, float scale, float lift, float from = -0.98f, float to = 0.98f, int count = 40)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(from, to, i / (float)count);
                var s = At(h, t);
                pts.Add(new Vector3(side * s.width * scale, s.top + lift, s.z));
            }
            return pts;
        }

        static void Rod(BoatMesh m, Material mat, IList<Vector3> pts, float radius, int sides = 8)
        {
            for (int i = 1; i < pts.Count; i++) m.Cylinder(mat, pts[i - 1], pts[i], radius, sides, false);
            foreach (var p in pts) m.Sphere(mat, p, Vector3.one * radius, 3, sides);
        }

        /// <summary>Mép mạn gỗ ốp hai bên ngoài và trong.</summary>
        static void Gunwales(BoatMesh m, BoatMesh.HullShape h, Material mat, float radius = 0.024f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Rod(m, mat, Sheer(h, side, 1.02f, -0.012f), radius);
                Rod(m, mat, Sheer(h, side, 0.84f, -0.02f, -0.92f, 0.92f), radius * 0.7f);
            }
        }

        /// <summary>Sườn gỗ cong bên trong lòng thuyền, cách đều nhau.</summary>
        static void Ribs(BoatMesh m, BoatMesh.HullShape h, Material mat, float spacing, float from = -0.8f, float to = 0.8f)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt((to - from) * h.length * 0.5f / spacing));
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(from, to, i / (float)n);
                var pts = new List<Vector3>();
                for (int k = 0; k <= 12; k++)
                {
                    float theta = Mathf.Lerp(-Mathf.PI * 0.47f, Mathf.PI * 0.47f, k / 12f);
                    var p = Outer(h, t, theta, 0.84f); p.y += 0.085f;
                    pts.Add(p);
                }
                Rod(m, mat, pts, 0.016f, 6);
            }
        }

        /// <summary>Mắt thuyền vẽ ở hai bên mũi theo kiểu thuyền gỗ Việt Nam.</summary>
        static void BowEyes(BoatMesh m, BoatMesh.HullShape h, float t, float height = 0.55f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var s = At(h, t);
                float y = Mathf.Lerp(s.top, s.bottom, 0.35f) * height + s.top * (1 - height);
                var p = new Vector3(side * (s.width * 1.0f + 0.004f), y, s.z);
                m.Sphere(Red, p, new Vector3(0.006f, 0.065f, 0.105f), 6, 16);
                m.Sphere(White, p + new Vector3(side * 0.004f, 0, 0), new Vector3(0.006f, 0.048f, 0.08f), 6, 16);
                m.Sphere(Black, p + new Vector3(side * 0.008f, 0.002f, 0.012f), new Vector3(0.006f, 0.03f, 0.03f), 6, 14);
            }
        }

        /// <summary>Dây buộc quấn vòng quanh một thanh, dùng cho bè tre, thúng, độc mộc.</summary>
        static void Lashing(BoatMesh m, Vector3 center, Vector3 axis, float radius, int turns = 4)
        {
            axis.Normalize();
            var side = Vector3.Cross(axis, Vector3.up); if (side.sqrMagnitude < 1e-3f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize(); var up = Vector3.Cross(side, axis);
            var pts = new List<Vector3>();
            for (int i = 0; i <= turns * 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                pts.Add(center + axis * ((i / (float)(turns * 10) - 0.5f) * turns * 0.022f) + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            Rod(m, Rope, pts, 0.007f, 5);
        }

        static void Basket(BoatMesh m, Vector3 bottom, float radius, float height)
        {
            m.Cylinder(Weave, bottom, bottom + Vector3.up * height, radius, 14);
            var rim = new List<Vector3>();
            for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.PI * 2; rim.Add(bottom + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius)); }
            Rod(m, BambooDark, rim, 0.012f, 5);
        }

        static void Lantern(BoatMesh m, Vector3 top)
        {
            m.Cylinder(Rope, top, top - Vector3.up * 0.12f, 0.004f, 4);
            m.Sphere(Paper, top - Vector3.up * 0.25f, new Vector3(0.11f, 0.13f, 0.11f), 8, 14);
            m.Cylinder(Gold, top - Vector3.up * 0.13f, top - Vector3.up * 0.15f, 0.05f, 10);
            m.Cylinder(Gold, top - Vector3.up * 0.37f, top - Vector3.up * 0.39f, 0.05f, 10);
            m.Cone(Gold, top - Vector3.up * 0.39f, top - Vector3.up * 0.48f, 0.012f, 6);
        }

        // ---------- Chi tiết riêng từng thuyền ----------

        static void AddDetails(Transform model, Spec spec, BoatMesh.HullShape? hullShape)
        {
            var m = new BoatMesh();
            var h = hullShape ?? default;
            switch (spec.id)
            {
                case "XuongBaLa":
                    Gunwales(m, h, WoodDark); Ribs(m, h, Wood, 0.42f); BowEyes(m, h, 0.8f);
                    Basket(m, new Vector3(0.12f, -0.2f, 1.15f), 0.13f, 0.2f);
                    break;
                case "DocMoc":
                    Gunwales(m, h, Bark, 0.03f);
                    foreach (float t in new[] { -0.9f, 0.9f })
                    {
                        var s = At(h, t);
                        Lashing(m, new Vector3(0, s.top - 0.02f, s.z), Vector3.right, s.width * 1.05f + 0.02f, 5);
                    }
                    Basket(m, new Vector3(0f, -0.22f, -0.05f), 0.15f, 0.22f);
                    m.Cylinder(WoodDark, new Vector3(-0.2f, -0.15f, 0.1f), new Vector3(0.25f, -0.13f, 1.9f), 0.018f, 6); // cây sào để dọc lòng
                    break;
                case "ThuyenRong":
                    Gunwales(m, h, Gold, 0.022f); Ribs(m, h, WoodLight, 0.55f, -0.85f, 0.75f);
                    DragonHead(m, new Vector3(0, 0.3f, 3.0f));
                    DragonTail(m, new Vector3(0, 0.3f, -3.1f));
                    foreach (float z in new[] { -1.7f, -2.2f })
                        foreach (float side in new[] { -1f, 1f })
                        {
                            var b = new Vector3(side * 0.42f, 0.2f, z);
                            m.Cylinder(WoodDark, b, b + Vector3.up * 0.9f, 0.012f, 5);
                            m.DoubleTri(side > 0 ? Gold : Red, b + Vector3.up * 0.9f, b + Vector3.up * 0.62f, b + new Vector3(0, 0.76f, -0.38f));
                        }
                    // Trống ở mũi: mặt da và đinh vàng
                    m.Cylinder(Hat, new Vector3(0f, 0.2f, 2.65f), new Vector3(0f, 0.21f, 2.65f), 0.16f, 18);
                    for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; m.Sphere(Gold, new Vector3(Mathf.Cos(a) * 0.17f, 0.18f, 2.65f + Mathf.Sin(a) * 0.17f), Vector3.one * 0.011f, 4, 6); }
                    break;
                case "TamBan":
                    Gunwales(m, h, Red, 0.026f); Ribs(m, h, Wood, 0.45f, -0.85f, 0.85f); BowEyes(m, h, 0.83f, 0.5f);
                    Lantern(m, new Vector3(0f, 1.02f, 0.78f));
                    Basket(m, new Vector3(-0.35f, -0.2f, 0.3f), 0.16f, 0.24f);
                    Basket(m, new Vector3(0.3f, -0.2f, -0.2f), 0.14f, 0.2f);
                    m.Sphere(Hat, new Vector3(0.1f, -0.12f, 0.2f), new Vector3(0.2f, 0.15f, 0.3f), 8, 14); // bao gạo
                    m.Beam(WoodDark, new Vector3(0.62f, 0.25f, -2.0f), new Vector3(0.62f, 0.62f, -2.0f), 0.06f, 0.06f); // cọc tựa mái chèo
                    break;
                case "ThuyenBuom":
                    Gunwales(m, h, WoodDark, 0.02f);
                    var top = new Vector3(0f, 3.95f, 0.6f);
                    m.Cylinder(Steel, top, new Vector3(0f, 0.32f, 1.88f), 0.006f, 4);
                    foreach (float side in new[] { -1f, 1f }) m.Cylinder(Steel, top, new Vector3(side * 0.6f, 0.3f, 0.45f), 0.006f, 4);
                    m.DoubleTri(Sail, new Vector3(0f, 0.55f, 1.8f), new Vector3(0f, 3.4f, 0.75f), new Vector3(0f, 0.6f, 0.75f)); // buồm mũi
                    m.Box(WoodDark, new Vector3(0f, -0.18f, -2.02f), new Vector3(0.04f, 0.6f, 0.32f)); // bánh lái
                    // Cần lái kéo dài tới tay phải người lái, tay trái giữ dây buồm từ đầu cần buồm
                    m.Beam(WoodDark, new Vector3(0f, 0.22f, -1.95f), new Vector3(0.21f, 0.23f, -0.8f), 0.04f, 0.03f);
                    m.Sphere(Dark, new Vector3(0.21f, 0.23f, -0.8f), Vector3.one * 0.03f, 4, 8);
                    var sheet = new List<Vector3>();
                    for (int i = 0; i <= 8; i++) { float u = i / 8f; sheet.Add(Vector3.Lerp(new Vector3(0f, 0.93f, -1.42f), new Vector3(-0.21f, 0.22f, -0.8f), u) - Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.08f); }
                    Rod(m, Rope, sheet, 0.006f, 4);
                    break;
                case "CaNoMay":
                    Gunwales(m, h, Steel, 0.016f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        // Lan can mũi bằng inox và cọc buộc dây
                        var rail = Sheer(h, side, 0.8f, 0.2f, 0.55f, 0.98f, 10);
                        Rod(m, Steel, rail, 0.012f, 6);
                        foreach (var p in new[] { rail[0], rail[5] }) m.Cylinder(Steel, p, p - Vector3.up * 0.2f, 0.01f, 6);
                        var cleat = Sheer(h, side, 0.85f, 0.015f, -0.85f, -0.85f, 1)[0];
                        m.Box(Steel, cleat, new Vector3(0.03f, 0.03f, 0.14f));
                    }
                    // Khung kính chắn gió
                    m.Cylinder(Dark, new Vector3(-0.6f, 0.45f, 0.6f), new Vector3(-0.6f, 0.8f, 0.42f), 0.015f, 6);
                    m.Cylinder(Dark, new Vector3(0.6f, 0.45f, 0.6f), new Vector3(0.6f, 0.8f, 0.42f), 0.015f, 6);
                    m.Cylinder(Dark, new Vector3(-0.6f, 0.8f, 0.42f), new Vector3(0.6f, 0.8f, 0.42f), 0.015f, 6);
                    // Máy đuôi: vỏ máy bo tròn, chân máy, tấm chắn và chân vịt
                    m.Sphere(Dark, new Vector3(0f, 0.55f, -2.12f), new Vector3(0.2f, 0.24f, 0.26f), 10, 16);
                    m.Sphere(White, new Vector3(0f, 0.62f, -2.12f), new Vector3(0.205f, 0.06f, 0.265f), 6, 16);
                    m.Box(Dark, new Vector3(0f, -0.45f, -2.16f), new Vector3(0.24f, 0.02f, 0.3f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i / 3f * Mathf.PI * 2;
                        m.Sphere(Steel, new Vector3(Mathf.Cos(a) * 0.08f, -0.56f + Mathf.Sin(a) * 0.08f, -2.25f), new Vector3(0.05f, 0.05f, 0.012f), 4, 8);
                    }
                    // Ghế bọc nệm
                    m.Box(Blue, new Vector3(0f, 0.16f, -0.95f), new Vector3(1.2f, 0.12f, 0.45f));
                    m.Box(Blue, new Vector3(0f, 0.4f, -1.15f), new Vector3(1.2f, 0.4f, 0.1f));
                    break;
                case "Kayak":
                    // Nắp khoang chứa đồ, dây viền boong, quai xách hai đầu, đường gân sống thuyền
                    m.Sphere(Dark, new Vector3(0f, 0.2f, 1.2f), new Vector3(0.16f, 0.025f, 0.22f), 6, 16);
                    m.Sphere(Dark, new Vector3(0f, 0.2f, -1.3f), new Vector3(0.18f, 0.025f, 0.26f), 6, 16);
                    foreach (float side in new[] { -1f, 1f }) Rod(m, Dark, Sheer(h, side, 0.95f, 0.005f, -0.85f, 0.85f, 24), 0.006f, 4);
                    foreach (float z in new[] { 2.08f, -2.08f })
                    {
                        var loop = new List<Vector3>();
                        for (int i = 0; i <= 8; i++) { float a = i / 8f * Mathf.PI; loop.Add(new Vector3(Mathf.Cos(a) * 0.05f, 0.16f + Mathf.Sin(a) * 0.04f, z)); }
                        Rod(m, Dark, loop, 0.008f, 5);
                    }
                    Rod(m, Dark, new List<Vector3> { new(0, -0.215f, -1.6f), new(0, -0.22f, 0f), new(0, -0.215f, 1.6f) }, 0.012f, 6);
                    break;
                case "ThuyenThung":
                    // Dây mây buộc vành, thanh ngồi và nan tre xương bên trong
                    for (int i = 0; i < 24; i++)
                    {
                        float a = i / 24f * Mathf.PI * 2;
                        Lashing(m, new Vector3(Mathf.Cos(a) * 0.97f, 0.12f, Mathf.Sin(a) * 0.97f), new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)), 0.05f, 2);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2;
                        var pts = new List<Vector3>();
                        for (int k = 0; k <= 6; k++) { float r = Mathf.Lerp(0.2f, 0.86f, k / 6f); float y = Mathf.Lerp(-0.33f, 0.08f, Mathf.Pow(k / 6f, 2.2f)); pts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); }
                        Rod(m, BambooDark, pts, 0.013f, 5);
                    }
                    m.Box(WoodDark, new Vector3(0f, -0.08f, 0.35f), new Vector3(1.4f, 0.04f, 0.2f));
                    break;
                case "BeTre":
                    // Dây buộc ở mỗi chỗ thanh ngang đè lên cây tre, lan can tre phía sau, gùi hàng
                    foreach (float z in new[] { -1.5f, 0f, 1.5f })
                        for (int i = 0; i < 8; i += 2) Lashing(m, new Vector3(-0.98f + i * 0.28f, 0.11f, z), Vector3.right, 0.13f, 3);
                    foreach (float x in new[] { -0.9f, 0.9f }) m.Cylinder(BambooDark, new Vector3(x, 0.15f, -1.85f), new Vector3(x, 0.85f, -1.85f), 0.03f, 6);
                    m.Cylinder(Bamboo, new Vector3(-0.95f, 0.82f, -1.85f), new Vector3(0.95f, 0.82f, -1.85f), 0.03f, 6);
                    Basket(m, new Vector3(-0.55f, 0.15f, 0.9f), 0.17f, 0.32f);
                    Basket(m, new Vector3(-0.5f, 0.15f, 1.4f), 0.14f, 0.26f);
                    break;
                case "PhaoCaoSu":
                    // Dây bám quanh phao võng giữa các khuyên chữ D, van bơm, ống ngồi tròn thay khối vuông
                    var grab = new List<Vector3>();
                    const float straight = 1.1f, radius = 0.72f;
                    var outline = new List<Vector3>();
                    for (int i = 0; i <= 8; i++) { float a = Mathf.PI * i / 8f; outline.Add(new Vector3(Mathf.Cos(a) * radius, 0, straight + Mathf.Sin(a) * radius)); }
                    for (int i = 0; i <= 8; i++) { float a = Mathf.PI + Mathf.PI * i / 8f; outline.Add(new Vector3(Mathf.Cos(a) * radius, 0, -straight + Mathf.Sin(a) * radius)); }
                    for (int i = 0; i < outline.Count; i++)
                    {
                        var p = outline[i]; var dir = new Vector3(p.x, 0, p.z - Mathf.Clamp(p.z, -straight, straight)).normalized;
                        var ring = p + dir * 0.25f + Vector3.up * 0.1f;
                        m.Sphere(Dark, ring, new Vector3(0.03f, 0.03f, 0.03f), 4, 8);
                        var q = outline[(i + 1) % outline.Count]; var dq = new Vector3(q.x, 0, q.z - Mathf.Clamp(q.z, -straight, straight)).normalized;
                        var next = q + dq * 0.25f + Vector3.up * 0.1f;
                        for (int k = 0; k <= 4; k++) { float u = k / 4f; grab.Add(Vector3.Lerp(ring, next, u) - Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.06f); }
                    }
                    Rod(m, Rope, grab, 0.009f, 5);
                    m.Cylinder(Dark, new Vector3(0.7f, 0.3f, 0.9f), new Vector3(0.7f, 0.34f, 0.9f), 0.03f, 8);
                    m.Cylinder(Dark, new Vector3(-0.7f, 0.3f, -0.9f), new Vector3(-0.7f, 0.34f, -0.9f), 0.03f, 8);
                    m.Cylinder(Orange, new Vector3(-0.65f, 0.02f, -0.4f), new Vector3(0.65f, 0.02f, -0.4f), 0.11f, 12);
                    break;
            }
            if (m.VertexCount == 0) return;
            var mesh = m.Build("Details_" + spec.id, out var mats); AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
            var go = new GameObject("BoatDetails"); go.transform.SetParent(model, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        /// <summary>Đầu rồng: cổ cong, sọ, mõm dài, hàm mở có răng, mắt lồi, sừng cong ra sau, bờm và râu.</summary>
        static void DragonHead(BoatMesh m, Vector3 b)
        {
            // Cổ cong vươn lên phía trước
            var neck = new List<Vector3>();
            for (int i = 0; i <= 8; i++) { float u = i / 8f; neck.Add(b + new Vector3(0, u * 0.8f, Mathf.Sin(u * Mathf.PI * 0.5f) * 0.45f - u * u * 0.1f)); }
            for (int i = 1; i < neck.Count; i++) m.Cylinder(DragonScales, neck[i - 1], neck[i], Mathf.Lerp(0.15f, 0.1f, i / 8f), 12);
            var head = neck[neck.Count - 1] + new Vector3(0, 0.08f, 0.05f);
            m.Sphere(Red, head, new Vector3(0.16f, 0.15f, 0.2f), 10, 16);
            m.Sphere(Red, head + new Vector3(0, -0.01f, 0.25f), new Vector3(0.11f, 0.08f, 0.2f), 10, 16); // mõm trên
            m.Sphere(Gold, head + new Vector3(0, 0.02f, 0.43f), new Vector3(0.07f, 0.05f, 0.05f), 8, 12); // chóp mũi
            m.Sphere(Red, head + new Vector3(0, -0.13f, 0.2f), new Vector3(0.09f, 0.04f, 0.18f), 8, 14); // hàm dưới hé mở
            for (int i = 0; i < 5; i++) foreach (float s in new[] { -1f, 1f })
                m.Cone(White, head + new Vector3(s * 0.07f, -0.06f, 0.12f + i * 0.06f), head + new Vector3(s * 0.07f, -0.1f, 0.12f + i * 0.06f), 0.012f, 5);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Sphere(Gold, head + new Vector3(s * 0.12f, 0.08f, 0.06f), new Vector3(0.05f, 0.05f, 0.05f), 8, 12);
                m.Sphere(Black, head + new Vector3(s * 0.155f, 0.085f, 0.08f), new Vector3(0.022f, 0.03f, 0.025f), 6, 10);
                // Sừng cong ra sau
                var horn = new List<Vector3>();
                for (int k = 0; k <= 5; k++) { float u = k / 5f; horn.Add(head + new Vector3(s * (0.07f + u * 0.12f), 0.12f + u * 0.22f, -0.05f - u * u * 0.25f)); }
                for (int k = 1; k < horn.Count; k++) m.Cylinder(Gold, horn[k - 1], horn[k], Mathf.Lerp(0.03f, 0.008f, k / 5f), 6);
                // Râu dài cong xuống
                var whisker = new List<Vector3>();
                for (int k = 0; k <= 6; k++) { float u = k / 6f; whisker.Add(head + new Vector3(s * (0.09f + u * 0.2f), -0.02f - u * u * 0.3f, 0.36f - u * 0.15f)); }
                Rod(m, Gold, whisker, 0.007f, 4);
            }
            // Bờm vàng chạy dọc gáy
            for (int i = 2; i < neck.Count; i++)
            {
                var p = neck[i]; var back = (neck[i] - neck[i - 1]).normalized;
                m.Cone(Gold, p + new Vector3(0, 0.02f, -0.08f), p + new Vector3(0, 0.12f, -0.22f) - back * 0.05f, 0.04f, 5);
            }
        }

        /// <summary>Đuôi rồng: thân thu nhỏ cong lên rồi xoè thành vây.</summary>
        static void DragonTail(BoatMesh m, Vector3 b)
        {
            var tail = new List<Vector3>();
            for (int i = 0; i <= 6; i++) { float u = i / 6f; tail.Add(b + new Vector3(0, u * 0.65f, -Mathf.Sin(u * Mathf.PI * 0.5f) * 0.4f)); }
            for (int i = 1; i < tail.Count; i++) m.Cylinder(DragonScales, tail[i - 1], tail[i], Mathf.Lerp(0.12f, 0.05f, i / 6f), 10);
            var tip = tail[tail.Count - 1];
            for (int k = -2; k <= 2; k++)
                m.DoubleTri(k % 2 == 0 ? Gold : Red, tip, tip + new Vector3(k * 0.1f, 0.42f, -0.25f + Mathf.Abs(k) * 0.05f), tip + new Vector3(k * 0.1f + 0.08f, 0.3f, -0.4f));
        }
    }
}
