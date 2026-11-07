using UnityEngine;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    static class NaturalTentMesh
    {
        public static Mesh Build()
        {
            var g = new G();
            const int arches = 24, rows = 12;
            // Continuous rounded canvas with a slight sag between its poles.
            for (int row = 0; row <= rows; row++)
                for (int a = 0; a <= arches; a++)
                {
                    float angle = a * Mathf.PI / arches;
                    float z = Mathf.Lerp(-1.5f, 1.5f, row / (float)rows);
                    float sag = 1f - 0.025f * Mathf.Sin(row * Mathf.PI / 3);
                    var p = new Vector3(Mathf.Cos(angle) * 1.15f, Mathf.Sin(angle) * 1.65f * sag, z);
                    p.y += Mathf.Sin(angle) * Mathf.Sin(angle * 7 + row) * 0.012f;
                    g.vertices.Add(p);
                    g.normals.Add(new Vector3(Mathf.Cos(angle) / 1.15f, Mathf.Sin(angle) / 1.65f, 0).normalized);
                    g.uv.Add(new Vector2(a / (float)arches * 2, row / (float)rows * 2));
                }
            for (int row = 0; row < rows; row++)
                for (int a = 0; a < arches; a++)
                {
                    int k = row * (arches + 1) + a;
                    g.triangles[0].AddRange(new[] { k, k + arches + 1, k + 1, k + 1, k + arches + 1, k + arches + 2 });
                }
            for (int a = 0; a < arches; a++)
            {
                float angle = a * Mathf.PI / arches, next = (a + 1) * Mathf.PI / arches;
                var outerA = new Vector3(Mathf.Cos(angle) * 1.15f, Mathf.Sin(angle) * 1.65f, -1.505f);
                var outerB = new Vector3(Mathf.Cos(next) * 1.15f, Mathf.Sin(next) * 1.65f, -1.505f);
                var innerA = new Vector3(Mathf.Cos(angle) * 0.56f, Mathf.Sin(angle) * 1.30f, -1.51f);
                var innerB = new Vector3(Mathf.Cos(next) * 0.56f, Mathf.Sin(next) * 1.30f, -1.51f);
                Face(g, outerA, innerA, innerB, 0); Face(g, outerA, innerB, outerB, 0);
                // Back is closed, front has an arched doorway.
                Face(g, new Vector3(0, 0.3f, 1.5f), outerB + Vector3.forward * 3.01f, outerA + Vector3.forward * 3.01f, 0);
            }
            Face(g, new Vector3(-1.14f, 0.02f, -1.5f), new Vector3(-1.14f, 0.02f, 1.5f), new Vector3(1.14f, 0.02f, 1.5f), 1);
            Face(g, new Vector3(-1.14f, 0.02f, -1.5f), new Vector3(1.14f, 0.02f, 1.5f), new Vector3(1.14f, 0.02f, -1.5f), 1);
            // Flexible pole sleeves and the contrasting door seam.
            foreach (float z in new[] { -1.51f, 0f, 1.51f })
                for (int a = 0; a < 16; a++)
                {
                    float aa = a * Mathf.PI / 16, ab = (a + 1) * Mathf.PI / 16;
                    var p = new Vector3(Mathf.Cos(aa) * 1.16f, Mathf.Sin(aa) * 1.67f, z);
                    var q = new Vector3(Mathf.Cos(ab) * 1.16f, Mathf.Sin(ab) * 1.67f, z);
                    g.Ellipsoid((p + q) * 0.5f, new Vector3(0.018f, (p - q).magnitude * 0.54f, 0.018f), Quaternion.FromToRotation(Vector3.up, q - p), 1, 6, 4);
                }
            var mesh = new Mesh { name = "CanvasCampTent" };
            mesh.SetVertices(g.vertices); mesh.SetNormals(g.normals); mesh.SetUVs(0, g.uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(g.triangles[0], 0); mesh.SetTriangles(g.triangles[1], 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Face(G g, Vector3 a, Vector3 b, Vector3 c, int mat)
        {
            int k = g.vertices.Count;
            var normal = Vector3.Cross(b - a, c - a).normalized;
            g.vertices.AddRange(new[] { a, b, c });
            g.normals.AddRange(new[] { normal, normal, normal });
            g.uv.AddRange(new[] { new Vector2(a.x, a.y), new Vector2(b.x, b.y), new Vector2(c.x, c.y) });
            g.triangles[mat].AddRange(new[] { k, k + 1, k + 2 });
            // Front fabric is visible from inside the doorway too.
            g.triangles[mat].AddRange(new[] { k + 2, k + 1, k });
        }
    }
}
