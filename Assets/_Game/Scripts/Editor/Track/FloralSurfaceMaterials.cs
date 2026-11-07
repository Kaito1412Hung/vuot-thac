using UnityEditor;
using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    static class FloralSurfaceMaterials
    {
        public static void Setup()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/FlowerWhite.mat");
            var petal = Texture("PetalPigment", false);
            var leaf = Texture("LeafVeins", true);
            foreach (string name in new[] { "FlowerWhite", "FlowerPink", "FlowerYellow", "FlowerPurple" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(TrackMaterials.Folder + "/" + name + ".mat");
                m.SetTexture("_BaseMap", petal);
                m.SetFloat("_Smoothness", 0.24f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
            }
            Material pollen = Material("FlowerPollen", source);
            pollen.color = new Color(0.94f, 0.55f, 0.065f);
            pollen.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(pollen);
            Material leaves = Material("BotanicalLeaves", source);
            leaves.color = new Color(0.20f, 0.37f, 0.10f);
            leaves.SetTexture("_BaseMap", leaf);
            leaves.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(leaves);
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name, Material source)
        {
            string path = TrackMaterials.Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(source) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Texture2D Texture(string name, bool leaf)
        {
            string path = "Assets/_Game/Generated/" + name + ".asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            const int size = 256;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Trilinear, anisoLevel = 8, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    float grain = Mathf.PerlinNoise(u * 42, v * 31);
                    float vein = Mathf.Pow(Mathf.Abs(Mathf.Cos((u * 18 + v * 0.7f) * Mathf.PI)), 18);
                    float shade = leaf ? 0.70f + 0.18f * grain + 0.12f * vein : 0.84f + 0.10f * grain + 0.06f * vein;
                    pixels[y * size + x] = new Color(shade, shade, shade, 1);
                }
            tex.SetPixels(pixels);
            tex.Apply(true, false);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }
    }
}
