using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    static class VillagerMeshes
    {
        const string Path = "Assets/_Game/Generated/Villagers.asset";
        public static Dictionary<string, Mesh> Load()
        {
            var stored = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Mesh>().ToDictionary(m => m.name);

            var list = new List<Mesh>();
            list.Add(Torso()); list.Add(Head()); list.Add(Hat());
            list.Add(Tapered("UpperSleeve", 0.30f, 0.075f, 0.055f));
            list.Add(Tapered("Forearm", 0.27f, 0.048f, 0.034f));
            list.Add(Tapered("TrouserThigh", 0.39f, 0.095f, 0.066f));
            list.Add(Tapered("TrouserShin", 0.37f, 0.065f, 0.047f));
            list.Add(Boot());
            list.Add(Hand("WorkHandL", -1));
            list.Add(Hand("WorkHandR", 1));
            var g = new G();
            Oval(g, 0, new Vector3(0, 0, 0), new Vector3(0.18f, 0.105f, 0.115f));
            list.Add(Finish(g, "TrouserHips", 1));
            if (AssetDatabase.LoadMainAssetAtPath(Path) == null) AssetDatabase.CreateAsset(new Mesh { name = "Villagers" }, Path);
            foreach (var mesh in list)
            {
                if (stored.TryGetValue(mesh.name, out var existing))
                {
                    existing.Clear(); existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
                    existing.subMeshCount = mesh.subMeshCount;
                    for (int i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i);
                    existing.RecalculateBounds(); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh);
                }
                else AssetDatabase.AddObjectToAsset(mesh, Path);
            }
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Mesh>().ToDictionary(m => m.name);
        }

        static void Oval(G g, int material, Vector3 p, Vector3 radii, Vector3 euler = default)
            => g.Ellipsoid(p, radii, Quaternion.Euler(euler), material, 16, 10);

        static Mesh Finish(G g, string name, int slots)
        {
            var m = new Mesh { name = name };
            m.SetVertices(g.vertices); m.SetNormals(g.normals); m.SetUVs(0, g.uv);
            m.subMeshCount = slots;
            for (int i = 0; i < slots; i++) m.SetTriangles(g.triangles[i], i);
            m.RecalculateBounds(); return m;
        }

        static Mesh Tapered(string name, float length, float top, float bottom)
        {
            var g = new G();
            const int sides = 24, rings = 20;
            bool shin = name.Contains("Shin"), forearm = name == "Forearm";
            for (int r = 0; r <= rings; r++)
                for (int i = 0; i <= sides; i++)
                {
                    float t = r / (float)rings, angle = i * Mathf.PI * 2 / sides;
                    float shape = shin ? 0.30f : forearm ? 0.24f : 0.32f;
                    float bulge = shin ? 0.018f : forearm ? 0.008f : 0.010f;
                    float radius = Mathf.Lerp(top, bottom, t) + bulge * Mathf.Exp(-Mathf.Pow((t - shape) / 0.24f, 2));
                    float depth = forearm ? 0.84f : 0.94f;
                    g.vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -length * t, Mathf.Sin(angle) * radius * depth));
                    g.normals.Add(new Vector3(Mathf.Cos(angle), (bottom - top) / length, Mathf.Sin(angle) / depth).normalized);
                    g.uv.Add(new Vector2(i / (float)sides, t));
                }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a = r * (sides + 1) + i, b = a + sides + 1;
                    g.triangles[0].AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            // Small joint caps fit inside the adjacent sleeve or trouser section.
            Oval(g, 0, Vector3.zero, new Vector3(top, 0.018f, top * 0.94f));
            Oval(g, 0, Vector3.down * length, new Vector3(bottom, 0.012f, bottom * 0.94f));
            return Finish(g, name, 1);
        }

        static Mesh Hand(string name, float side)
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, -0.028f, 0), new Vector3(0.032f, 0.043f, 0.020f));
            for (int finger = 0; finger < 4; finger++)
            {
                float x = (finger - 1.5f) * 0.016f;
                float extra = finger == 1 || finger == 2 ? 0.008f : 0;
                Phalanx(g, new Vector3(x, -0.058f, 0), new Vector3(x, -0.082f - extra, 0.012f), 0.008f);
                Phalanx(g, new Vector3(x, -0.082f - extra, 0.012f), new Vector3(x, -0.085f - extra, 0.035f), 0.007f);
                Phalanx(g, new Vector3(x, -0.085f - extra, 0.035f), new Vector3(x, -0.070f - extra, 0.042f), 0.006f);
            }
            Phalanx(g, new Vector3(side * 0.025f, -0.018f, 0), new Vector3(side * 0.044f, -0.045f, 0.018f), 0.011f);
            Phalanx(g, new Vector3(side * 0.044f, -0.045f, 0.018f), new Vector3(side * 0.028f, -0.061f, 0.037f), 0.009f);
            return Finish(g, name, 1);
        }

        static void Phalanx(G g, Vector3 a, Vector3 b, float radius)
            => g.Ellipsoid((a + b) * 0.5f, new Vector3(radius, (b - a).magnitude * 0.64f, radius), Quaternion.FromToRotation(Vector3.up, b - a), 0, 10, 6);

        static Mesh Boot()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, -0.015f, 0.055f), new Vector3(0.054f, 0.046f, 0.118f));
            Oval(g, 0, new Vector3(0, 0.035f, -0.005f), new Vector3(0.049f, 0.070f, 0.055f));
            Oval(g, 1, new Vector3(0, -0.049f, 0.052f), new Vector3(0.055f, 0.014f, 0.12f));
            return Finish(g, "LeatherBoot", 2);
        }

        static Mesh Torso()
        {
            var g = new G();
            float[] heights = { -0.015f, 0.08f, 0.22f, 0.38f, 0.47f, 0.52f };
            float[] widths = { 0.17f, 0.165f, 0.16f, 0.20f, 0.235f, 0.15f };
            float[] depths = { 0.115f, 0.12f, 0.115f, 0.13f, 0.12f, 0.09f };
            const int sides = 24;
            for (int row = 0; row < heights.Length; row++)
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    g.vertices.Add(new Vector3(Mathf.Cos(a) * widths[row], heights[row], Mathf.Sin(a) * depths[row]));
                    g.normals.Add(new Vector3(Mathf.Cos(a) / widths[row], 0, Mathf.Sin(a) / depths[row]).normalized);
                    g.uv.Add(new Vector2(i / (float)sides, row / 5f));
                }
            for (int row = 0; row < heights.Length - 1; row++)
                for (int i = 0; i < sides; i++)
                {
                    int a = row * (sides + 1) + i, b = a + sides + 1;
                    g.triangles[0].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            Oval(g, 0, new Vector3(0, 0.48f, 0), new Vector3(0.20f, 0.055f, 0.105f));
            return Finish(g, "WorkShirt", 1);
        }

        static Mesh Head()
        {
            var g = new G();
            // A continuous head surface, with sculpted jaw, cheeks, eye sockets and nose.
            const int rows = 64, sides = 80;
            float[] ys = { -0.005f, 0.025f, 0.060f, 0.105f, 0.160f, 0.215f, 0.255f, 0.285f, 0.300f };
            float[] widths = { 0.025f, 0.057f, 0.075f, 0.092f, 0.097f, 0.094f, 0.080f, 0.050f, 0.001f };
            float[] depths = { 0.045f, 0.065f, 0.080f, 0.094f, 0.101f, 0.099f, 0.087f, 0.055f, 0.001f };
            for (int row = 0; row <= rows; row++)
                for (int i = 0; i <= sides; i++)
                {
                    float y = Mathf.Lerp(ys[0], ys[ys.Length - 1], row / (float)rows);
                    int band = 0; while (band < ys.Length - 2 && y > ys[band + 1]) band++;
                    float width = Profile(ys, widths, band, y);
                    float depth = Profile(ys, depths, band, y);
                    float angle = i * Mathf.PI * 2 / sides;
                    float x = Mathf.Cos(angle) * width, z = Mathf.Sin(angle) * depth;
                    if (z > 0)
                    {
                        float front = Mathf.Pow(Mathf.Sin(angle), 6);
                        float bridge = Gaussian(x, y, 0, 0.156f, 0.016f, 0.041f) * 0.021f;
                        float tip = Gaussian(x, y, 0, 0.126f, 0.020f, 0.016f) * 0.023f;
                        float sockets = (Gaussian(x, y, -0.040f, 0.178f, 0.024f, 0.016f) + Gaussian(x, y, 0.040f, 0.178f, 0.024f, 0.016f)) * 0.010f;
                        float cheek = (Gaussian(x, y, -0.052f, 0.120f, 0.023f, 0.026f) + Gaussian(x, y, 0.052f, 0.120f, 0.023f, 0.026f)) * 0.007f;
                        z += (bridge + tip - sockets + cheek) * front;
                    }
                    g.vertices.Add(new Vector3(x, y, z)); g.normals.Add(Vector3.zero);
                    g.uv.Add(new Vector2(i / (float)sides, row / (float)rows));
                }
            for (int row = 0; row < rows; row++)
                for (int i = 0; i < sides; i++)
                {
                    int a = row * (sides + 1) + i, b = a + sides + 1;
                    g.triangles[0].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            Oval(g, 0, new Vector3(0, -0.012f, -0.005f), new Vector3(0.047f, 0.070f, 0.045f));
            foreach (float s in new[] { -1f, 1f })
            {
                Oval(g, 0, new Vector3(s * 0.098f, 0.152f, -0.006f), new Vector3(0.014f, 0.031f, 0.020f));
                Oval(g, 3, new Vector3(s * 0.040f, 0.178f, 0.096f), new Vector3(0.016f, 0.0065f, 0.005f));
                Oval(g, 5, new Vector3(s * 0.040f, 0.178f, 0.1005f), new Vector3(0.0048f, 0.0058f, 0.002f));
                Oval(g, 2, new Vector3(s * 0.040f, 0.178f, 0.102f), new Vector3(0.0026f, 0.0039f, 0.001f));
                Oval(g, 1, new Vector3(s * 0.041f, 0.198f, 0.094f), new Vector3(0.020f, 0.0028f, 0.003f), new Vector3(0, 0, -s * 5));
                Oval(g, 0, new Vector3(s * 0.040f, 0.186f, 0.097f), new Vector3(0.018f, 0.004f, 0.004f));

            }
            Oval(g, 4, new Vector3(0, 0.077f, 0.092f), new Vector3(0.023f, 0.003f, 0.005f));
            Oval(g, 4, new Vector3(0, 0.071f, 0.093f), new Vector3(0.021f, 0.0034f, 0.004f));
            int hairStart = g.vertices.Count;
            for (int row = 0; row <= 16; row++)
                for (int i = 0; i <= 64; i++)
                {
                    float theta = i * Mathf.PI * 2 / 64;
                    float maxPhi = 1.65f - 0.50f * Mathf.Max(0, Mathf.Sin(theta)) + 0.035f * Mathf.Sin(theta * 3);
                    float phi = maxPhi * row / 16f;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    g.vertices.Add(new Vector3(unit.x * 0.096f, 0.19f + unit.y * 0.117f, -0.008f + unit.z * 0.101f));
                    g.normals.Add(unit); g.uv.Add(new Vector2(i / 64f, row / 16f));
                }
            for (int row = 0; row < 16; row++)
                for (int i = 0; i < 64; i++)
                {
                    int a = hairStart + row * 65 + i, b = a + 65;
                    if (row > 0) g.triangles[1].AddRange(new[] { a, a + 1, b });
                    g.triangles[1].AddRange(new[] { a + 1, b + 1, b });
                }
            var mesh = Finish(g, "FarmerHead", 6);
            mesh.RecalculateNormals();
            return mesh;
        }

        static float Profile(float[] ys, float[] values, int i, float y)
        {
            float h = ys[i + 1] - ys[i], t = Mathf.Clamp01((y - ys[i]) / h);
            int before = Mathf.Max(0, i - 1), after = Mathf.Min(ys.Length - 1, i + 2);
            float m0 = (values[i + 1] - values[before]) / (ys[i + 1] - ys[before]);
            float m1 = (values[after] - values[i]) / (ys[after] - ys[i]);
            return Mathf.Max(0.001f, (2*t*t*t - 3*t*t + 1)*values[i] + (t*t*t - 2*t*t + t)*h*m0
                + (-2*t*t*t + 3*t*t)*values[i+1] + (t*t*t - t*t)*h*m1);
        }

        static float Gaussian(float x, float y, float cx, float cy, float sx, float sy)
            => Mathf.Exp(-Mathf.Pow((x - cx) / sx, 2) - Mathf.Pow((y - cy) / sy, 2));

        static Mesh Hat()
        {
            var g = new G();
            const int sides = 48;
            for (int row = 0; row <= 8; row++)
                for (int i = 0; i <= sides; i++)
                {
                    float t = row / 8f, angle = i * Mathf.PI * 2 / sides;
                    float radius = Mathf.Lerp(0.30f, 0.018f, t);
                    g.vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0.275f + t * 0.17f, Mathf.Sin(angle) * radius));
                    g.normals.Add(new Vector3(Mathf.Cos(angle) * 0.51f, 0.86f, Mathf.Sin(angle) * 0.51f));
                    g.uv.Add(new Vector2(i / (float)sides * 3, t * 2));
                }
            for (int row = 0; row < 8; row++)
                for (int i = 0; i < sides; i++)
                {
                    int a = row * (sides + 1) + i, b = a + sides + 1;
                    g.triangles[0].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                    g.triangles[0].AddRange(new[] { a, b + 1, b, a, a + 1, b + 1 });
                }
            return Finish(g, "WovenConicalHat", 1);
        }
    }
}
