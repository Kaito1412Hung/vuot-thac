using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VuotThac.EditorTools.Boats
{
    /// <summary>
    /// Sinh texture cho thuyền bằng code: ảnh màu (gần như trung tính để vật liệu tô màu lên) và normal map
    /// tính từ bản đồ độ cao, để ánh sáng bắt vào khe ván, sợi đan, đốt tre… Tất cả lặp liền mạch (tile).
    /// UV của thuyền tính theo mét: u dọc thân thuyền, v vòng quanh mặt cắt (xem BoatMesh.Build).
    /// </summary>
    public static class BoatTextures
    {
        public const string Folder = "Assets/_Game/Textures/Boats";
        const int Size = 512;

        public static Texture2D Albedo(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
        public static Texture2D Normal(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_N.png");

        [MenuItem("Vượt Thác/Thuyền/Sinh lại texture thuyền")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            Save("Planks", Planks(false));
            Save("PaintedPlanks", Planks(true));
            Save("Adzed", Adzed());
            Save("Weave", Weave(10, false));
            Save("WeaveFine", Weave(22, true));
            Save("Bamboo", Bamboo());
            Save("Rubber", Rubber());
            Save("Sailcloth", Sailcloth());
            Save("DragonScales", Scales());
            Save("Gelcoat", Gelcoat());
            AssetDatabase.Refresh();
            foreach (var n in new[] { "Planks", "PaintedPlanks", "Adzed", "Weave", "WeaveFine", "Bamboo", "Rubber", "Sailcloth", "DragonScales", "Gelcoat" })
            {
                Import($"{Folder}/{n}.png", false);
                Import($"{Folder}/{n}_N.png", true);
            }
            AssetDatabase.Refresh();
        }

        // ---------- Ghi file và cài đặt nhập ----------

        struct Maps { public Color[] color; public float[] height; public float bump; }

        static void Save(string name, Maps maps)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.SetPixels(maps.color); tex.Apply();
            File.WriteAllBytes($"{Folder}/{name}.png", tex.EncodeToPNG());
            var n = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float l = H(maps.height, x - 1, y), r = H(maps.height, x + 1, y), d = H(maps.height, x, y - 1), u = H(maps.height, x, y + 1);
                    var v = new Vector3((l - r) * maps.bump, (d - u) * maps.bump, 1f).normalized;
                    n[y * Size + x] = new Color(v.x * .5f + .5f, v.y * .5f + .5f, v.z * .5f + .5f);
                }
            tex.SetPixels(n); tex.Apply();
            File.WriteAllBytes($"{Folder}/{name}_N.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static float H(float[] h, int x, int y) => h[((y % Size + Size) % Size) * Size + (x % Size + Size) % Size];

        static void Import(string path, bool normal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 512;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
        }

        // ---------- Nhiễu lặp liền mạch ----------

        static float Hash(int x, int y, int seed)
        {
            unchecked { int h = x * 374761393 + y * 668265263 + seed * 2147483647; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }
        }

        /// <summary>Nhiễu giá trị lặp sau <paramref name="period"/> ô.</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed), c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Nhiễu nhiều tầng, toạ độ u,v từ 0 đến 1, lặp liền mạch. px, py là số ô theo mỗi chiều ở tầng đầu.</summary>
        static float Fbm(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                int fx = px << o, fy = py << o;
                // Hai chiều có chu kỳ khác nhau: dùng chu kỳ theo chiều x cho cả hai rồi co giãn y
                sum += amp * NoiseXY(u * fx, v * fy, fx, fy, seed + o * 31); norm += amp; amp *= 0.5f;
            }
            return sum / norm;
        }

        static float NoiseXY(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int WX(int v) => ((v % periodX) + periodX) % periodX; int WY(int v) => ((v % periodY) + periodY) % periodY;
            float a = Hash(WX(x0), WY(y0), seed), b = Hash(WX(x0 + 1), WY(y0), seed), c = Hash(WX(x0), WY(y0 + 1), seed), d = Hash(WX(x0 + 1), WY(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static Maps New(float bump) => new Maps { color = new Color[Size * Size], height = new float[Size * Size], bump = bump };

        static void Each(Maps m, Func<float, float, (Color c, float h)> f)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var r = f((x + .5f) / Size, (y + .5f) / Size);
                    m.color[y * Size + x] = r.c; m.height[y * Size + x] = r.h;
                }
        }

        // ---------- Các loại bề mặt ----------

        /// <summary>Ván gỗ ghép dọc thân: 6 tấm theo chiều v, vân chạy theo u, khe ván, mối nối, đinh, mắt gỗ.</summary>
        static Maps Planks(bool painted)
        {
            var m = New(painted ? 4f : 6f);
            const int planks = 6;
            Each(m, (u, v) =>
            {
                float pv = v * planks; int plank = Mathf.FloorToInt(pv); float inPlank = pv - plank;
                // Mỗi tấm có một mối nối đầu ván ở vị trí khác nhau
                float butt = Hash(plank, 3, 11); float du = Mathf.Repeat(u - butt, 1f);
                int piece = du < 0.5f ? 0 : 1;
                float tone = Hash(plank, piece, 5) * 0.18f - 0.09f;
                float grain = Fbm(u, v, 3, 48, 4, plank * 7 + piece);
                float streak = Mathf.Abs(Mathf.Sin((v * planks * 9f + grain * 6f) * Mathf.PI));
                float knot = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float ku = Hash(plank, k + piece * 2, 21), kv = (plank + 0.3f + 0.4f * Hash(plank, k, 23)) / planks;
                    var d = new Vector2(Mathf.DeltaAngle(u * 360, ku * 360) / 360f * 4f, (v - kv) * planks);
                    if (Hash(plank, k, 29) > 0.55f) knot = Mathf.Max(knot, Mathf.Clamp01(1 - d.magnitude * 9f));
                }
                float seam = Mathf.Clamp01(1 - Mathf.Min(inPlank, 1 - inPlank) * planks * 14f);
                float buttSeam = Mathf.Clamp01(1 - Mathf.Min(du, 1 - du, Mathf.Abs(du - 0.5f)) * 220f);
                float nail = 0f;
                foreach (float cu in new[] { 0.012f, 0.988f, 0.512f, 0.488f })
                {
                    var d = new Vector2((du - cu) * 4f, (inPlank - 0.5f) / planks);
                    nail = Mathf.Max(nail, Mathf.Clamp01(1 - d.magnitude * 160f));
                }
                float wood = 0.78f + tone + (grain - 0.5f) * 0.25f - streak * 0.06f - knot * 0.35f;
                var woodColor = new Color(wood, wood * 0.93f, wood * 0.86f);
                Color c = woodColor; float h = 0.5f + (grain - 0.5f) * 0.15f - knot * 0.1f;
                if (painted)
                {
                    float wear = Fbm(u, v, 6, 6, 4, 77);
                    float chip = Mathf.SmoothStep(0.62f, 0.7f, wear + seam * 0.25f + buttSeam * 0.2f);
                    float brush = 0.95f + (Fbm(u, v, 2, 60, 3, 91) - 0.5f) * 0.07f;
                    var paint = new Color(brush, brush, brush);
                    c = Color.Lerp(paint, woodColor * 0.75f, chip);
                    h = 0.55f - chip * 0.08f + (brush - 0.95f) * 0.3f;
                }
                c *= 1f - seam * 0.55f - buttSeam * 0.45f;
                c = Color.Lerp(c, new Color(0.25f, 0.24f, 0.24f), nail * 0.9f);
                h -= seam * 0.45f + buttSeam * 0.3f; h += nail * 0.2f;
                return (c, h);
            });
            return m;
        }

        /// <summary>Gỗ đẽo bằng rìu: những vết lõm hình vảy nối nhau, vân gỗ chạy dọc.</summary>
        static Maps Adzed()
        {
            var m = New(7f);
            Each(m, (u, v) =>
            {
                float grain = Fbm(u, v, 2, 40, 4, 3);
                // Mỗi vết rìu là một lõm hình elip dài theo u
                float gu = u * 10f, gv = v * 6f; int cu = Mathf.FloorToInt(gu), cv = Mathf.FloorToInt(gv);
                float best = 9f;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cu + dx, y = cv + dy;
                    float jx = Hash(((x % 10) + 10) % 10, ((y % 6) + 6) % 6, 41), jy = Hash(((x % 10) + 10) % 10, ((y % 6) + 6) % 6, 43);
                    var d = new Vector2((gu - x - jx) * 0.8f, (gv - y - jy) * 1.4f);
                    best = Mathf.Min(best, d.sqrMagnitude);
                }
                float scoop = Mathf.Clamp01(best * 1.6f);
                float w = 0.62f + (grain - 0.5f) * 0.3f + scoop * 0.12f;
                return (new Color(w, w * 0.9f, w * 0.8f), scoop * 0.6f + grain * 0.15f);
            });
            return m;
        }

        /// <summary>Nan tre đan nong mốt: các nan ngang dọc luồn trên dưới. <paramref name="strips"/> nan mỗi chiều.</summary>
        static Maps Weave(int strips, bool fine)
        {
            var m = New(fine ? 3f : 5f);
            Each(m, (u, v) =>
            {
                float su = u * strips, sv = v * strips; int iu = Mathf.FloorToInt(su), iv = Mathf.FloorToInt(sv);
                float fu = su - iu, fv = sv - iv;
                // Đan 2 lên 2 xuống, lệch nhau mỗi hàng: kiểu đan thúng chai
                bool horizontalOnTop = ((iu + iv) / 2 + iv) % 2 == 0;
                float edgeU = Mathf.Min(fu, 1 - fu), edgeV = Mathf.Min(fv, 1 - fv);
                float strand = horizontalOnTop ? edgeV : edgeU;
                float along = horizontalOnTop ? fu : fv;
                float bulge = Mathf.Sin(Mathf.Clamp01(strand * 2.2f) * Mathf.PI * 0.5f);
                float gap = Mathf.Clamp01(1 - Mathf.Min(edgeU, edgeV) * 18f) * 0.6f;
                float fiber = Fbm(horizontalOnTop ? u : v, horizontalOnTop ? v : u, 4, strips * 6, 3, horizontalOnTop ? iv : iu + 100);
                float tone = Hash(horizontalOnTop ? iv : iu, horizontalOnTop ? 1 : 2, 13) * 0.16f;
                float w = 0.72f + tone + (fiber - 0.5f) * 0.25f - (1 - bulge) * 0.25f;
                var c = new Color(w, w * 0.9f, w * 0.68f) * (1 - gap);
                float h = (horizontalOnTop ? 0.6f : 0.4f) + bulge * 0.35f - gap * 0.4f + Mathf.Sin(along * Mathf.PI) * 0.1f;
                return (c, h);
            });
            return m;
        }

        /// <summary>Thân tre: đốt tre đều theo u, sọc mảnh dọc thân, vết ố quanh đốt.</summary>
        static Maps Bamboo()
        {
            var m = New(6f);
            Each(m, (u, v) =>
            {
                float nodes = u * 3f; float fn = nodes - Mathf.Floor(nodes);
                float dist = Mathf.Min(fn, 1 - fn);
                float ring = Mathf.Clamp01(1 - dist * 40f);
                float stain = Mathf.Clamp01(1 - dist * 7f);
                float stripes = Fbm(u, v, 1, 64, 3, 5);
                float age = Fbm(u, v, 3, 3, 3, 9);
                float w = 0.82f + (stripes - 0.5f) * 0.12f - stain * 0.12f - ring * 0.25f;
                var c = new Color(w, w * 0.92f, w * 0.62f);
                c = Color.Lerp(c, new Color(0.55f, 0.5f, 0.35f), Mathf.SmoothStep(0.55f, 0.75f, age) * 0.5f);
                return (c, 0.5f + ring * 0.4f - stain * 0.05f + stripes * 0.05f);
            });
            return m;
        }

        /// <summary>Vải cao su phao: hạt li ti, đường dán nối tấm theo u, vết trầy nhẹ.</summary>
        static Maps Rubber()
        {
            var m = New(2.5f);
            Each(m, (u, v) =>
            {
                float grain = Fbm(u, v, 64, 64, 2, 7);
                float panel = u * 4f; float fp = panel - Mathf.Floor(panel);
                float seam = Mathf.Clamp01(1 - Mathf.Abs(fp - 0.5f) * 90f);
                float tape = Mathf.Clamp01(1 - Mathf.Abs(fp - 0.5f) * 16f);
                float scuff = Mathf.SmoothStep(0.62f, 0.78f, Fbm(u, v, 5, 5, 4, 19));
                float w = 0.9f + (grain - 0.5f) * 0.06f - tape * 0.05f - seam * 0.2f;
                var c = Color.Lerp(new Color(w, w, w), new Color(0.75f, 0.73f, 0.7f), scuff * 0.35f);
                return (c, 0.5f + grain * 0.12f + tape * 0.15f - seam * 0.3f);
            });
            return m;
        }

        /// <summary>Vải buồm: sợi dệt mịn, đường may ghép tấm theo v, vết ố.</summary>
        static Maps Sailcloth()
        {
            var m = New(2f);
            Each(m, (u, v) =>
            {
                float weave = (Mathf.Sin(u * Size * Mathf.PI * 0.5f) * Mathf.Sin(v * Size * Mathf.PI * 0.5f)) * 0.5f + 0.5f;
                float panel = v * 4f; float fp = panel - Mathf.Floor(panel);
                float seam = Mathf.Clamp01(1 - Mathf.Abs(fp - 0.5f) * 60f);
                float stitch = seam * (Mathf.Repeat(u * 160f, 1f) < 0.5f ? 1 : 0.3f);
                float stain = Mathf.SmoothStep(0.6f, 0.85f, Fbm(u, v, 3, 3, 4, 33));
                float w = 0.96f - weave * 0.04f - seam * 0.08f - stain * 0.08f;
                return (new Color(w, w * 0.98f, w * 0.92f) * (1 - stitch * 0.15f), 0.5f + weave * 0.08f + seam * 0.25f);
            });
            return m;
        }

        /// <summary>Vảy rồng sơn: vảy hình bán nguyệt xếp so le, viền vàng, lòng vảy đỏ đậm dần.</summary>
        static Maps Scales()
        {
            var m = New(5f);
            const int cols = 10, rows = 20; const float radius = 0.62f;
            Each(m, (u, v) =>
            {
                // Mỗi vảy là một hình tròn; hàng sau (r lớn hơn) đè lên nửa dưới hàng trước như vảy cá
                float su = u * cols, sy = v * rows * 0.5f;
                int baseRow = Mathf.FloorToInt(sy / 0.5f);
                float bestD = 9f; int bestRow = int.MinValue;
                for (int r = baseRow - 2; r <= baseRow + 2; r++)
                {
                    float shift = ((r % 2) + 2) % 2 == 0 ? 0f : 0.5f;
                    float cy = r * 0.5f;
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float cx = Mathf.Floor(su - shift) + 0.5f + shift + ox;
                        float d = new Vector2(su - cx, (sy - cy) * 1.15f).magnitude;
                        if (d < radius && r > bestRow) { bestRow = r; bestD = d; }
                    }
                }
                float t = Mathf.Clamp01(bestD / radius);
                float rim = Mathf.SmoothStep(0.86f, 0.95f, t);
                var inner = Color.Lerp(new Color(0.82f, 0.12f, 0.07f), new Color(0.42f, 0.03f, 0.03f), Mathf.SmoothStep(0.25f, 0.88f, t));
                var c = Color.Lerp(inner, new Color(0.93f, 0.7f, 0.22f), rim * 0.9f);
                float wear = Fbm(u, v, 4, 4, 3, 61);
                c = Color.Lerp(c, c * 0.8f, Mathf.SmoothStep(0.6f, 0.8f, wear));
                return (c, 0.65f - t * 0.45f + rim * 0.15f);
            });
            return m;
        }

        /// <summary>Vỏ composite sơn bóng: gần như phẳng, chỉ có hạt rất mịn và vết trầy mờ.</summary>
        static Maps Gelcoat()
        {
            var m = New(1.2f);
            Each(m, (u, v) =>
            {
                float speck = Fbm(u, v, 96, 96, 2, 3);
                float scratch = Mathf.Clamp01(1 - Mathf.Abs(Mathf.Sin((u * 9 + Fbm(u, v, 2, 2, 2, 7) * 3) * Mathf.PI * 4)) * 60f) * Mathf.SmoothStep(0.55f, 0.7f, Fbm(u, v, 3, 3, 3, 11));
                float w = 0.97f - speck * 0.03f - scratch * 0.06f;
                return (new Color(w, w, w), 0.5f + speck * 0.05f - scratch * 0.2f);
            });
            return m;
        }
    }
}
