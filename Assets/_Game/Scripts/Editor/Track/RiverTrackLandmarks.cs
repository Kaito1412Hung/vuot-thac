using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static void BuildLandmarks(BuildContext ctx, Transform parent)
        {
            BuildWindmill(ctx, parent);

        }

        static void SceneryBeam(Transform parent, string name, Material mat, Vector3 a, Vector3 b, float radius)
        {
            var beam = Primitive(PrimitiveType.Cylinder, name, mat, parent);
            beam.localPosition = (a + b) * 0.5f;
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            beam.localScale = new Vector3(radius * 2, (b - a).magnitude * 0.5f, radius * 2);
        }

        static void BuildWindmill(BuildContext ctx, Transform parent)
        {
            Vector3 p = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool found = false;
            for (int i = 0; i < 24; i++)
            {
                var s = ctx.Track.EvaluateAtDistance(ctx.Track.StartDistance + ctx.Track.RaceLength * (0.54f + i * 0.007f));
                float side = i % 2 == 0 ? -1 : 1;
                p = s.center + s.right * side * (ctx.Track.HalfWidth + ctx.Track.bankWidth + 25);
                float d; p.y = ctx.Track.TerrainHeight(p, out d);
                if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 14 || ctx.IsExcluded(p, 9)) continue;
                p.y = MaxTerrain(ctx.Track, p, 3.4f);
                rotation = Quaternion.LookRotation(s.right * side, Vector3.up);
                found = true; break;
            }
            if (!found) return;
            var root = Group(parent, "HilltopWindmill");
            root.SetPositionAndRotation(p, rotation);
            var tower = new G();
            const int sides = 32;
            for (int level = 0; level <= 12; level++)
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    float t = level / 12f, radius = Mathf.Lerp(2.65f, 1.25f, t);
                    tower.vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -1.3f + t * 12, Mathf.Sin(angle) * radius));
                    tower.normals.Add(new Vector3(Mathf.Cos(angle), 0.115f, Mathf.Sin(angle)).normalized);
                    tower.uv.Add(new Vector2(i / (float)sides * 6, t * 6));
                }
            for (int level = 0; level < 12; level++)
                for (int i = 0; i < sides; i++)
                {
                    int a = level * (sides + 1) + i, b = a + sides + 1;
                    tower.triangles[0].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            var mesh = FinishGeometry(tower, "TaperedWindmillTower", 1);
            ctx.Meshes.Add(mesh);
            CreateMeshObject("MasonryTower", mesh, new[] { ctx.Mats.Plaster }, root);
            var cap = new FlatMeshBuilder(1);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24, b = (i + 1) * Mathf.PI * 2 / 24;
                cap.TriOutward(0, new Vector3(Mathf.Cos(a) * 1.75f, 10.65f, Mathf.Sin(a) * 1.75f), new Vector3(0, 13.0f, 0), new Vector3(Mathf.Cos(b) * 1.75f, 10.65f, Mathf.Sin(b) * 1.75f), Vector3.up * 11);
            }
            var roof = cap.ToMesh("WindmillRoof"); ctx.Meshes.Add(roof);
            CreateMeshObject("CapRoof", roof, new[] { ctx.Mats.Roofs[3] }, root);
            DetailBox(root, "Entrance", ctx.Mats.WoodDark, new Vector3(0, 1, -2.52f), new Vector3(1.2f, 2, 0.16f));
            var glass = AccentMaterial("CottageGlass", new Color(0.2f, 0.39f, 0.43f), ctx.Mats.Wood, false);
            foreach (float y in new[] { 4f, 7f })
                DetailBox(root, "TowerWindow", glass, new Vector3(0, y, -(2.65f - (y + 1.3f) / 12f * 1.4f)), new Vector3(0.7f, 1.05f, 0.12f));
            var rotor = Group(root, "WindSails");
            rotor.localPosition = new Vector3(0, 9.5f, -2.15f);
            var sailCanvas = AccentMaterial("WindSailLinen", new Color(0.89f, 0.84f, 0.65f), ctx.Mats.Plaster, false);
            for (int sail = 0; sail < 4; sail++)
            {
                var arm = Group(rotor, "Sail_" + sail);
                arm.localRotation = Quaternion.Euler(0, 0, sail * 90 + 25);
                DetailBox(arm, "Spar", ctx.Mats.WoodDark, new Vector3(0, 3.2f, 0), new Vector3(0.15f, 6.3f, 0.18f));
                DetailBox(arm, "Canvas", sailCanvas, new Vector3(0.5f, 4.15f, 0.04f), new Vector3(0.95f, 3.6f, 0.045f));
                foreach (float x in new[] { 0.1f, 1.02f })
                    DetailBox(arm, "SailEdge", ctx.Mats.Wood, new Vector3(x, 4.1f, -0.02f), new Vector3(0.065f, 3.8f, 0.1f));
                for (int j = 0; j < 9; j++)
                    DetailBox(arm, "SailRib", ctx.Mats.WoodDark, new Vector3(0.53f, 2.4f + j * 0.43f, -0.04f), new Vector3(1.12f, 0.055f, 0.10f));
            }
            var hub = Primitive(PrimitiveType.Sphere, "IronHub", ctx.Mats.AnimalDark, rotor);
            hub.localScale = new Vector3(0.55f, 0.55f, 0.45f);
            rotor.gameObject.AddComponent<World.Rotator>().degreesPerSecond = new Vector3(0, 0, -13);
            ctx.Exclude(p, 10);
            ctx.Count("cối xay gió", 1);
        }

        static Mesh FinishGeometry(G g, string name, int slots)
        {
            var m = new Mesh { name = name };
            m.SetVertices(g.vertices); m.SetNormals(g.normals); m.SetUVs(0, g.uv);
            m.subMeshCount = slots;
            for (int i = 0; i < slots; i++) m.SetTriangles(g.triangles[i], i);
            m.RecalculateBounds(); return m;
        }

        static Material FlowMaterial(string name, Color tint, float speed, float foam)
        {
            string path = TrackMaterials.Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("VuotThac/ScenicFlow")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", tint); mat.SetFloat("_Speed", speed); mat.SetFloat("_Foam", foam);
            EditorUtility.SetDirty(mat); return mat;
        }

        static void Spray(Transform root, string name, Vector3 position, bool splash)
        {
            const string path = TrackMaterials.Folder + "/WaterSpray.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("VuotThac/SoftSpray")) { name = "WaterSpray" }; AssetDatabase.CreateAsset(mat, path); }
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 4; main.prewarm = true; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(splash ? 0.5f : 1.5f, splash ? 1.2f : 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(splash ? 1.5f : 0.15f, splash ? 3.5f : 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(splash ? 0.05f : 0.45f, splash ? 0.15f : 1.4f);
            main.startColor = new Color(0.88f, 0.97f, 1, splash ? 0.65f : 0.18f);
            main.gravityModifier = splash ? 0.7f : -0.02f; main.maxParticles = 160;
            var emission = ps.emission; emission.rateOverTime = splash ? 45 : 20;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = splash ? 55 : 25; shape.radius = 1.3f;
            var colors = ps.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Simulate(3, true, true); ps.Play();
        }
    }
}
