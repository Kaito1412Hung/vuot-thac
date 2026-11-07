using UnityEditor;
using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    static class NaturalSurfaceMaterials
    {
        public static void Setup()
        {
            Apply("Wood", "TimberGrain", 0); Apply("WoodDark", "TimberGrain", 0);
            Apply("Trunk", "BarkGrain", 1);
            Apply("Rock", "StoneGrain", 2); Apply("Cliff", "StoneGrain", 2);
            Apply("Grass", "MeadowGround", 3); Apply("GrassDark", "MeadowGround", 3);
            Apply("Sand", "SandGrain", 4);
            Apply("TentCanvas", "CanvasWeave", 6);
            foreach (string coat in new[] { "DeerBrown", "FoxOrange", "BearBrown", "RabbitGray", "DuckBrown" }) Apply(coat, "FurGrain", 5);
            var fox = AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/FoxOrange.mat");
            fox.color = new Color(0.66f, 0.31f, 0.11f); EditorUtility.SetDirty(fox);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PropLibrary.AssetPath))
            {
                var mesh = asset as Mesh;
                if (mesh == null || mesh.vertexCount == 0 || mesh.uv.Length != 0) continue;
                EnsureUV(mesh);
                EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
        }

        public static void EnsureUV(Mesh mesh)
        {
            var points = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i]; var n = normals[i];
                uv[i] = Mathf.Abs(n.y) > Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.z)) ? new Vector2(p.x, p.z) * 0.5f
                    : Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(p.z, p.y) * 0.5f : new Vector2(p.x, p.y) * 0.5f;
            }
            mesh.uv = uv;
        }

        static void Apply(string material, string texture, int kind)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/" + material + ".mat");
            if (mat == null) return;
            string path = "Assets/_Game/Generated/" + texture + ".asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                const int size = 512;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = texture, anisoLevel = 8, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat };
                var colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / (float)size, v = y / (float)size;
                        float broad = Mathf.PerlinNoise(u * 9 + 13, v * 9 + 7);
                        float fine = Mathf.PerlinNoise(u * 95, v * 95);
                        float shade;
                        if (kind < 2)
                        {
                            float grain = Mathf.PerlinNoise(u * (kind == 0 ? 45 : 70), v * 3);
                            shade = 0.62f + 0.30f * grain + 0.08f * fine;
                        }
                        else if (kind == 6) shade = 0.86f + 0.05f * Mathf.Sin(u * 512 * Mathf.PI) * Mathf.Sin(v * 512 * Mathf.PI) + 0.06f * fine;
                        else if (kind == 5) shade = 0.76f + 0.17f * Mathf.PerlinNoise(u * 160, v * 12) + 0.07f * fine;
                        else shade = 0.64f + 0.24f * broad + 0.12f * fine;
                        colors[y * size + x] = new Color(shade, shade, shade, 1);
                    }
                tex.SetPixels(colors); tex.Apply(true, false);
                AssetDatabase.CreateAsset(tex, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }
    }
}
