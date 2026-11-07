using System.Collections.Generic;
using UnityEngine;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    static class NaturalAnimalMeshes
    {
        static void Oval(G g, int mat, Vector3 p, Vector3 size, Vector3 angles = default)
            => g.Ellipsoid(p, size, Quaternion.Euler(angles), mat, 14, 8);

        static void Limb(G g, int mat, Vector3 a, Vector3 b, float radius, float width = 1)
            => g.Ellipsoid((a + b) * 0.5f, new Vector3(radius * width, (b - a).magnitude * 0.62f, radius), Quaternion.FromToRotation(Vector3.up, b - a), mat, 10, 6);

        static void Eyes(G g, Vector3 p, float spread, float size, int dark = 2)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Oval(g, dark, p + Vector3.right * side * spread, new Vector3(size * 0.55f, size, size));
                Oval(g, 1, p + new Vector3(side * (spread + size * 0.4f), size * 0.3f, size * 0.3f), Vector3.one * size * 0.23f);
            }
        }

        static Mesh Mesh(G g, string name, int slots = 3)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(g.vertices); mesh.SetNormals(g.normals); mesh.SetUVs(0, g.uv);
            mesh.subMeshCount = slots;
            for (int i = 0; i < slots; i++) mesh.SetTriangles(g.triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh[] BuildAll()
        {
            return new[] { Deer(), Leg("Deer_Leg", 0.76f, 0.05f), Fox(), Leg("Fox_Leg", 0.29f, 0.033f), Bear(), Leg("Bear_Leg", 0.55f, 0.12f), Rabbit(), Duck(), Bird(), Wing() };
        }

        static Mesh Deer()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, 0.97f, -0.03f), new Vector3(0.235f, 0.29f, 0.55f));
            Oval(g, 1, new Vector3(0, 0.78f, 0), new Vector3(0.19f, 0.09f, 0.38f));
            Oval(g, 0, new Vector3(0, 1.0f, -0.32f), new Vector3(0.25f, 0.29f, 0.24f));
            Limb(g, 0, new Vector3(0, 1.04f, 0.28f), new Vector3(0, 1.5f, 0.52f), 0.125f);
            Oval(g, 0, new Vector3(0, 1.52f, 0.61f), new Vector3(0.115f, 0.14f, 0.22f), new Vector3(12, 0, 0));
            Oval(g, 2, new Vector3(0, 1.48f, 0.815f), new Vector3(0.066f, 0.046f, 0.038f));
            Eyes(g, new Vector3(0, 1.57f, 0.64f), 0.106f, 0.022f);
            foreach (float s in new[] { -1f, 1f })
            {
                Oval(g, 0, new Vector3(s * 0.16f, 1.69f, 0.51f), new Vector3(0.065f, 0.15f, 0.045f), new Vector3(-12, 0, -s * 38));
                Oval(g, 1, new Vector3(s * 0.163f, 1.69f, 0.545f), new Vector3(0.035f, 0.1f, 0.008f), new Vector3(0, 0, -s * 38));
                var a = new Vector3(s * 0.065f, 1.63f, 0.52f);
                var b = a + new Vector3(s * 0.10f, 0.33f, -0.10f);
                Limb(g, 1, a, b, 0.021f);
                Limb(g, 1, b, b + new Vector3(s * 0.04f, 0.20f, 0.04f), 0.013f);
                Limb(g, 1, a + (b - a) * 0.6f, b + new Vector3(s * 0.13f, 0.08f, 0.10f), 0.014f);
                // A few small pale flank spots, kept flush with the coat.
                for (int i = 0; i < 6; i++)
                    Oval(g, 1, new Vector3(s * 0.213f, 1.07f + i % 2 * 0.055f, -0.3f + i * 0.10f), new Vector3(0.008f, 0.022f, 0.03f));
            }
            Oval(g, 1, new Vector3(0, 1.08f, -0.56f), new Vector3(0.06f, 0.11f, 0.07f), new Vector3(25, 0, 0));
            return Mesh(g, "Deer_Body");
        }

        static Mesh Fox()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, 0.41f, -0.02f), new Vector3(0.15f, 0.16f, 0.34f));
            Oval(g, 1, new Vector3(0, 0.4f, 0.23f), new Vector3(0.115f, 0.16f, 0.13f));
            Oval(g, 0, new Vector3(0, 0.57f, 0.34f), new Vector3(0.13f, 0.12f, 0.16f));
            Oval(g, 1, new Vector3(0, 0.52f, 0.49f), new Vector3(0.07f, 0.06f, 0.15f), new Vector3(10, 0, 0));
            Oval(g, 2, new Vector3(0, 0.535f, 0.62f), new Vector3(0.032f, 0.027f, 0.026f));
            Eyes(g, new Vector3(0, 0.60f, 0.41f), 0.112f, 0.018f);
            foreach (float s in new[] { -1f, 1f })
            {
                Oval(g, 0, new Vector3(s * 0.085f, 0.72f, 0.32f), new Vector3(0.06f, 0.115f, 0.04f), new Vector3(-10, 0, -s * 13));
                Oval(g, 2, new Vector3(s * 0.085f, 0.73f, 0.35f), new Vector3(0.035f, 0.065f, 0.012f));
            }
            Oval(g, 0, new Vector3(0, 0.35f, -0.57f), new Vector3(0.13f, 0.13f, 0.35f), new Vector3(-14, 0, 0));
            Oval(g, 1, new Vector3(0, 0.29f, -0.83f), new Vector3(0.09f, 0.09f, 0.13f), new Vector3(-14, 0, 0));
            return Mesh(g, "Fox_Body");
        }

        static Mesh Bear()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, 0.86f, -0.08f), new Vector3(0.42f, 0.43f, 0.74f));
            Oval(g, 0, new Vector3(0, 1.10f, 0.38f), new Vector3(0.36f, 0.35f, 0.36f));
            Oval(g, 0, new Vector3(0, 1.05f, 0.83f), new Vector3(0.27f, 0.26f, 0.29f));
            Oval(g, 1, new Vector3(0, 0.99f, 1.09f), new Vector3(0.15f, 0.115f, 0.16f));
            Oval(g, 2, new Vector3(0, 1.03f, 1.23f), new Vector3(0.074f, 0.05f, 0.038f));
            Eyes(g, new Vector3(0, 1.16f, 0.99f), 0.222f, 0.025f);
            foreach (float s in new[] { -1f, 1f })
                Oval(g, 0, new Vector3(s * 0.20f, 1.28f, 0.76f), new Vector3(0.087f, 0.10f, 0.05f));
            Oval(g, 0, new Vector3(0, 0.92f, -0.81f), new Vector3(0.07f, 0.08f, 0.08f));
            return Mesh(g, "Bear_Body");
        }

        static Mesh Leg(string name, float length, float radius)
        {
            var g = new G();
            Limb(g, 0, Vector3.zero, new Vector3(0, -length * 0.48f, -0.025f), radius * 1.35f);
            Limb(g, 0, new Vector3(0, -length * 0.46f, -0.025f), new Vector3(0, -length + 0.045f, 0.02f), radius * 0.75f);
            Oval(g, 1, new Vector3(0, -length + 0.025f, 0.035f), new Vector3(radius * 1.1f, 0.04f, radius * 1.45f));
            return Mesh(g, name, 2);
        }

        static Mesh Rabbit()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, 0.20f, -0.02f), new Vector3(0.14f, 0.17f, 0.22f));
            Oval(g, 0, new Vector3(0, 0.34f, 0.17f), new Vector3(0.105f, 0.11f, 0.12f));
            Oval(g, 1, new Vector3(0, 0.30f, 0.27f), new Vector3(0.07f, 0.045f, 0.05f));
            Oval(g, 2, new Vector3(0, 0.32f, 0.31f), new Vector3(0.018f, 0.012f, 0.008f));
            Eyes(g, new Vector3(0, 0.37f, 0.21f), 0.095f, 0.018f);
            foreach (float s in new[] { -1f, 1f })
            {
                Oval(g, 0, new Vector3(s * 0.055f, 0.55f, 0.13f), new Vector3(0.039f, 0.19f, 0.028f), new Vector3(-13, 0, -s * 11));
                Oval(g, 1, new Vector3(s * 0.055f, 0.55f, 0.151f), new Vector3(0.022f, 0.145f, 0.009f), new Vector3(-13, 0, -s * 11));
                Oval(g, 0, new Vector3(s * 0.10f, 0.12f, -0.10f), new Vector3(0.07f, 0.11f, 0.12f));
                Oval(g, 1, new Vector3(s * 0.10f, 0.035f, 0.0f), new Vector3(0.046f, 0.035f, 0.11f));
                Limb(g, 0, new Vector3(s * 0.07f, 0.22f, 0.12f), new Vector3(s * 0.07f, 0.035f, 0.18f), 0.029f);
            }
            Oval(g, 1, new Vector3(0, 0.23f, -0.24f), Vector3.one * 0.065f);
            return Mesh(g, "Rabbit");
        }

        static Mesh Duck()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0, 0.09f, -0.025f), new Vector3(0.145f, 0.12f, 0.24f));
            Oval(g, 1, new Vector3(0, 0.24f, 0.13f), new Vector3(0.055f, 0.13f, 0.065f), new Vector3(-12, 0, 0));
            Oval(g, 1, new Vector3(0, 0.34f, 0.17f), new Vector3(0.082f, 0.085f, 0.09f));
            Oval(g, 2, new Vector3(0, 0.32f, 0.28f), new Vector3(0.053f, 0.021f, 0.085f));
            Eyes(g, new Vector3(0, 0.36f, 0.20f), 0.073f, 0.012f, 3);
            foreach (float s in new[] { -1f, 1f })
                Oval(g, 0, new Vector3(s * 0.115f, 0.12f, -0.05f), new Vector3(0.035f, 0.075f, 0.17f), new Vector3(0, s * 10, 0));
            Oval(g, 0, new Vector3(0, 0.15f, -0.25f), new Vector3(0.08f, 0.035f, 0.1f), new Vector3(-18, 0, 0));
            return Mesh(g, "Duck", 4);
        }

        static Mesh Bird()
        {
            var g = new G();
            Oval(g, 0, Vector3.zero, new Vector3(0.065f, 0.055f, 0.18f));
            Oval(g, 0, new Vector3(0, 0.04f, 0.17f), new Vector3(0.047f, 0.045f, 0.065f));
            Oval(g, 0, new Vector3(0, 0, -0.21f), new Vector3(0.075f, 0.012f, 0.10f));
            return Mesh(g, "Bird_Body", 1);
        }

        static Mesh Wing()
        {
            var g = new G();
            Oval(g, 0, new Vector3(0.23f, 0, 0), new Vector3(0.24f, 0.015f, 0.085f), new Vector3(0, -10, 0));
            Oval(g, 0, new Vector3(0.45f, -0.01f, -0.055f), new Vector3(0.19f, 0.009f, 0.06f), new Vector3(0, 20, 0));
            return Mesh(g, "Bird_Wing", 1);
        }
    }
}
