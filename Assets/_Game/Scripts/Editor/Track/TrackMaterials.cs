using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Vật liệu màu trơn cho map low-poly. Lần đầu sẽ tạo file .mat, các lần sau chỉ nạp lại
    /// nên chỉnh màu trong Unity không bị ghi đè khi dựng lại địa hình.
    /// </summary>
    public class TrackMaterials
    {
        public const string Folder = "Assets/_Game/Materials";

        public Material Sand, Grass, GrassDark, Cliff, Rock, Trunk, Leaves, LeavesDark, Bush, Water, GatePole, GateStart, GateFinish;
        public Material Wood, WoodDark, Roof, Reed, LilyPad, Mountain, MountainRock, Cloud;
        public Material Snow, ForestFloor, Plaster, Hay;
        public Material DeerBrown, AnimalCream, AnimalDark, FoxOrange, BearBrown, RabbitGray, DuckBrown, DuckGreen, Beak, BirdGray;
        public Material[] Flowers;
        public Material[] Roofs;

        public static TrackMaterials LoadOrCreate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            return new TrackMaterials
            {
                Sand = Get("Sand", new Color(0.78f, 0.69f, 0.49f)),
                Grass = Get("Grass", new Color(0.44f, 0.64f, 0.29f)),
                Cliff = Get("Cliff", new Color(0.52f, 0.49f, 0.45f)),
                Rock = Get("Rock", new Color(0.46f, 0.45f, 0.43f)),
                Trunk = Get("Trunk", new Color(0.42f, 0.28f, 0.17f)),
                Leaves = Get("Leaves", new Color(0.27f, 0.52f, 0.25f)),
                LeavesDark = Get("LeavesDark", new Color(0.18f, 0.40f, 0.22f)),
                Water = Get("Water", new Color(0.18f, 0.52f, 0.68f, 0.82f), smoothness: 0.85f, transparent: true),
                GatePole = Get("GatePole", new Color(0.93f, 0.93f, 0.9f)),
                GateStart = Get("GateStart", new Color(0.25f, 0.72f, 0.33f)),
                GateFinish = Get("GateFinish", new Color(0.86f, 0.25f, 0.22f)),
                GrassDark = Get("GrassDark", new Color(0.36f, 0.56f, 0.25f)),
                Bush = Get("Bush", new Color(0.31f, 0.55f, 0.24f)),
                Wood = Get("Wood", new Color(0.62f, 0.44f, 0.27f)),
                WoodDark = Get("WoodDark", new Color(0.33f, 0.23f, 0.15f)),
                Roof = Get("Roof", new Color(0.66f, 0.27f, 0.2f)),
                Reed = Get("Reed", new Color(0.5f, 0.6f, 0.27f)),
                LilyPad = Get("LilyPad", new Color(0.3f, 0.6f, 0.3f)),
                Mountain = Get("Mountain", new Color(0.39f, 0.55f, 0.36f)),
                MountainRock = Get("MountainRock", new Color(0.5f, 0.5f, 0.5f)),
                Cloud = Get("Cloud", new Color(0.98f, 0.98f, 1f)),
                Snow = Get("Snow", new Color(0.94f, 0.96f, 0.99f), smoothness: 0.3f),
                ForestFloor = Get("ForestFloor", new Color(0.27f, 0.45f, 0.24f)),
                Plaster = Get("Plaster", new Color(0.9f, 0.85f, 0.72f)),
                Hay = Get("Hay", new Color(0.86f, 0.73f, 0.36f)),
                DeerBrown = Get("DeerBrown", new Color(0.58f, 0.38f, 0.21f)),
                AnimalCream = Get("AnimalCream", new Color(0.9f, 0.84f, 0.72f)),
                AnimalDark = Get("AnimalDark", new Color(0.13f, 0.1f, 0.08f)),
                FoxOrange = Get("FoxOrange", new Color(0.88f, 0.45f, 0.14f)),
                BearBrown = Get("BearBrown", new Color(0.32f, 0.21f, 0.13f)),
                RabbitGray = Get("RabbitGray", new Color(0.62f, 0.55f, 0.48f)),
                DuckBrown = Get("DuckBrown", new Color(0.55f, 0.43f, 0.32f)),
                DuckGreen = Get("DuckGreen", new Color(0.12f, 0.45f, 0.25f)),
                Beak = Get("Beak", new Color(0.98f, 0.62f, 0.15f)),
                BirdGray = Get("BirdGray", new Color(0.22f, 0.22f, 0.25f)),
                Roofs = new[]
                {
                    Get("Roof", new Color(0.66f, 0.27f, 0.2f)),
                    Get("RoofBlue", new Color(0.27f, 0.42f, 0.62f)),
                    Get("RoofGreen", new Color(0.3f, 0.5f, 0.32f)),
                    Get("RoofBrown", new Color(0.45f, 0.3f, 0.2f)),
                },
                Flowers = new[]
                {
                    Get("FlowerYellow", new Color(0.98f, 0.84f, 0.25f)),
                    Get("FlowerPink", new Color(0.95f, 0.5f, 0.68f)),
                    Get("FlowerWhite", new Color(0.97f, 0.97f, 0.95f)),
                    Get("FlowerPurple", new Color(0.62f, 0.45f, 0.85f)),
                },
            };
        }

        static Material Get(string name, Color color, float smoothness = 0.1f, bool transparent = false)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name, enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (transparent)
                MakeTransparent(mat);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }
    }
}
