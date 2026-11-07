using System.Collections.Generic;
using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    // Smooth botanical geometry with separate stems/leaves, petals and pollen.
    // Shared by all flower patches: no per-instance mesh allocations at runtime.
    static class BotanicalFlowerMesh
    {
        internal sealed class Geometry
        {
            public bool distant;
            public readonly List<Vector3> vertices = new();
            public readonly List<Vector3> normals = new();
            public readonly List<Vector2> uv = new();
            public readonly List<int>[] triangles = { new(), new(), new(), new(), new(), new() };

            public void Ellipsoid(Vector3 center, Vector3 radius, Quaternion rotation, int material, int sides = 8, int rings = 4)
            {
                if (distant) { sides = 4; rings = 2; }
                int start = vertices.Count;
                for (int y = 0; y <= rings; y++)
                    for (int x = 0; x <= sides; x++)
                    {
                        float phi = Mathf.PI * y / rings;
                        float theta = Mathf.PI * 2 * x / sides;
                        var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                        vertices.Add(center + rotation * Vector3.Scale(unit, radius));
                        normals.Add(rotation * new Vector3(unit.x / radius.x, unit.y / radius.y, unit.z / radius.z).normalized);
                        uv.Add(new Vector2(x / (float)sides, y / (float)rings));
                    }
                var t = triangles[material];
                for (int y = 0; y < rings; y++)
                    for (int x = 0; x < sides; x++)
                    {
                        int a = start + y * (sides + 1) + x, b = a + sides + 1;
                        if (y > 0) { t.Add(a); t.Add(a + 1); t.Add(b); }
                        if (y < rings - 1) { t.Add(a + 1); t.Add(b + 1); t.Add(b); }
                    }
            }

            public void Stem(Vector3 a, Vector3 b, float radius)
            {
                var q = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                int start = vertices.Count;
                for (int ring = 0; ring < 2; ring++)
                    for (int i = 0; i <= 6; i++)
                    {
                        float angle = i * Mathf.PI / 3;
                        var normal = q * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                        vertices.Add((ring == 0 ? a : b) + normal * radius * (ring == 0 ? 1 : 0.85f));
                        normals.Add(normal);
                        uv.Add(new Vector2(i / 6f, ring));
                    }
                for (int i = 0; i < 6; i++)
                {
                    int k = start + i;
                    triangles[0].AddRange(new[] { k, k + 7, k + 8, k, k + 8, k + 1 });
                }
            }
        }

        public static Mesh DistantMesh()
        {
            const string path = "Assets/_Game/Generated/FlowersDistant.asset";
            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = Build("FlowersDistant", 42, true);
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh Build(string name, int seed, bool distant = false)
        {
            var g = new Geometry { distant = distant };
            var rng = new System.Random(seed);
            for (int flower = 0; flower < 7; flower++)
            {
                float a = flower * 2.39996f;
                float radius = flower == 0 ? 0 : 0.36f + (float)rng.NextDouble() * 0.48f;
                var root = new Vector3(Mathf.Cos(a) * radius, -0.035f, Mathf.Sin(a) * radius);
                float height = 0.43f + (float)rng.NextDouble() * 0.37f;
                var lean = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.12f;
                var head = root + Vector3.up * height + lean;
                var middle = root + Vector3.up * height * 0.52f + lean * 0.28f;
                g.Stem(root, middle, 0.012f);
                g.Stem(middle, head, 0.010f);
                // Alternating lance-shaped leaves, tilted toward light.
                for (int leaf = 0; leaf < 3; leaf++)
                {
                    float angle = a + leaf * 2.5f;
                    var direction = new Vector3(Mathf.Cos(angle), 0.45f, Mathf.Sin(angle)).normalized;
                    var pos = Vector3.Lerp(root, head, 0.24f + leaf * 0.17f);
                    float length = leaf == 2 ? 0.12f : 0.18f;
                    g.Ellipsoid(pos + direction * length * 0.75f, new Vector3(0.044f, 0.008f, length), Quaternion.LookRotation(direction), 0);
                }
                var flowerRotation = Quaternion.Euler(lean.z * 95, a * Mathf.Rad2Deg, -lean.x * 95);
                int petals = flower % 3 == 0 ? 12 : 9;
                float petalLength = flower % 3 == 0 ? 0.115f : 0.135f;
                for (int petal = 0; petal < petals; petal++)
                {
                    float angle = petal * 360f / petals;
                    var rotation = flowerRotation * Quaternion.Euler(-9 + petal % 3 * 6, angle, 0);
                    var center = head + flowerRotation * (Quaternion.Euler(0, angle, 0) * new Vector3(0, 0.004f, petalLength * 0.86f));
                    g.Ellipsoid(center, new Vector3(petalLength * 0.33f, 0.012f, petalLength), rotation, 1);
                }
                g.Ellipsoid(head + flowerRotation * Vector3.up * 0.018f, new Vector3(0.057f, 0.029f, 0.057f), flowerRotation, 2, 12, 5);
                // Small raised pollen grains catch highlights at close range.
                for (int grain = 0; grain < (distant ? 0 : 7); grain++)
                {
                    float angle = grain * 2.39996f;
                    var offset = new Vector3(Mathf.Cos(angle) * 0.033f, 0.044f, Mathf.Sin(angle) * 0.033f);
                    g.Ellipsoid(head + flowerRotation * offset, Vector3.one * 0.008f, flowerRotation, 2, 5, 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(g.vertices);
            mesh.SetNormals(g.normals);
            mesh.SetUVs(0, g.uv);
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(g.triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
