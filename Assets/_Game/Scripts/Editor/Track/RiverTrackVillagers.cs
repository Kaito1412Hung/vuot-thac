using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static Material VillagerMaterial(BuildContext ctx, string name, Color color, bool fabric = false)
        {
            var m = AccentMaterial(name, color, ctx.Mats.Plaster, false);
            m.color = color;
            if (fabric) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedFolder + "/CanvasWeave.asset"));
            m.SetFloat("_Smoothness", fabric ? 0.08f : 0.25f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildVillagers(BuildContext ctx, Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/People/Grab3D/Source/man-base-character-rigged.fbx") != null)
            {
                BuildRealisticVillagers(ctx, parent);
                return;
            }
            var meshes = VillagerMeshes.Load();
            var skin = VillagerMaterial(ctx, "FarmerSkin", new Color(0.72f, 0.51f, 0.36f));
            var lips = VillagerMaterial(ctx, "FarmerLips", new Color(0.46f, 0.255f, 0.205f));
            var iris = VillagerMaterial(ctx, "FarmerIris", new Color(0.22f, 0.14f, 0.08f));
            var eyes = VillagerMaterial(ctx, "FarmerEyeWhite", new Color(0.69f, 0.69f, 0.61f));
            var hair = VillagerMaterial(ctx, "FarmerHair", new Color(0.10f, 0.075f, 0.055f));
            var gloves = VillagerMaterial(ctx, "FarmerGloves", new Color(0.47f, 0.35f, 0.22f));
            var pants = VillagerMaterial(ctx, "FarmerTrousers", new Color(0.15f, 0.20f, 0.25f), true);
            var boots = VillagerMaterial(ctx, "FarmerBoots", new Color(0.17f, 0.12f, 0.085f));
            var straw = VillagerMaterial(ctx, "FarmerStrawHat", new Color(0.78f, 0.62f, 0.34f), true);
            var apron = VillagerMaterial(ctx, "FarmerApron", new Color(0.36f, 0.24f, 0.14f), true);
            var shirts = new[] {
                VillagerMaterial(ctx, "FarmerShirtTeal", new Color(0.18f, 0.35f, 0.32f), true),
                VillagerMaterial(ctx, "FarmerShirtBlue", new Color(0.24f, 0.34f, 0.48f), true),
                VillagerMaterial(ctx, "FarmerShirtRust", new Color(0.48f, 0.25f, 0.19f), true)
            };
            var steel = VillagerMaterial(ctx, "ToolSteel", new Color(0.43f, 0.49f, 0.50f));
            var houses = ctx.Track.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && (m.sharedMesh.name == "Cabin" || m.sharedMesh.name == "HouseTall")).ToArray();
            int count = 0;
            for (int i = 0; i < houses.Length && count < 12; i += 2)
            {
                var house = houses[i].transform;
                var work = (World.RuralVillager.Work)(count % 3);
                var local = work == World.RuralVillager.Work.ChopWood ? new Vector3(5, 0, -2.5f)
                    : work == World.RuralVillager.Work.CarryFirewood ? new Vector3(-3, 0, -6.5f) : new Vector3(-4.5f, 0, -6.5f);
                Vector3 p = house.TransformPoint(local);
                float distance;
                p.y = ctx.Track.TerrainHeight(p, out distance);
                if (distance < ctx.Track.HalfWidth + ctx.Track.bankWidth + 1.5f) continue;
                Vector3 routeB = house.TransformPoint(new Vector3(3, 0, -6.5f));
                routeB.y = ctx.Track.TerrainHeight(routeB, out distance);
                if (work == World.RuralVillager.Work.CarryFirewood && (distance < ctx.Track.HalfWidth + ctx.Track.bankWidth + 1.5f || Mathf.Abs(routeB.y - p.y) > 1.5f)) continue;
                var root = Group(parent, "Villager_" + count.ToString("00") + "_" + work);
                root.SetPositionAndRotation(p, house.rotation * Quaternion.Euler(0, 180, 0));
                var ai = root.gameObject.AddComponent<World.RuralVillager>();
                ai.work = work; ai.phase = count * 0.69f; ai.routeA = p; ai.routeB = routeB;
                var shirt = shirts[count % shirts.Length];
                CreateMeshObject("Hips", meshes["TrouserHips"], new[] { pants }, root).transform.localPosition = Vector3.up * 0.9f;
                ai.torso = Group(root, "TorsoRig"); ai.torso.localPosition = Vector3.up * 0.91f;
                CreateMeshObject("TailoredWorkShirt", meshes["WorkShirt"], new[] { shirt }, ai.torso);
                DetailBox(ai.torso, "LeatherBelt", boots, new Vector3(0, 0.02f, 0.115f), new Vector3(0.30f, 0.045f, 0.025f));
                DetailBox(ai.torso, "BrassBuckle", straw, new Vector3(0, 0.02f, 0.137f), new Vector3(0.045f, 0.035f, 0.013f));
                // A small curved work apron, following the torso instead of a box.
                var ag = new G();
                ag.Ellipsoid(new Vector3(0, 0.15f, 0.12f), new Vector3(0.145f, 0.24f, 0.023f), Quaternion.identity, 0, 16, 8);
                var apronMesh = FinishGeometry(ag, "WorkApron_" + count, 1); ctx.Meshes.Add(apronMesh);
                CreateMeshObject("WorkApron", apronMesh, new[] { apron }, ai.torso);
                foreach (float x in new[] { -0.09f, 0.09f })
                    SceneryBeam(ai.torso, "ApronStrap", apron, new Vector3(x, 0.50f, 0.08f), new Vector3(x, 0.29f, 0.14f), 0.012f);
                ai.head = Group(ai.torso, "HeadRig"); ai.head.localPosition = new Vector3(0, 0.56f, 0);
                CreateMeshObject("FaceAndHair", meshes["FarmerHead"], new[] { skin, hair, ctx.Mats.AnimalDark, eyes, lips, iris }, ai.head);
                CreateMeshObject("WovenHat", meshes["WovenConicalHat"], new[] { straw }, ai.head);
                ai.upperArmL = BuildArm(ctx, meshes, ai.torso, -1, shirt, skin, gloves, out ai.lowerArmL);
                ai.upperArmR = BuildArm(ctx, meshes, ai.torso, 1, shirt, skin, gloves, out ai.lowerArmR);
                ai.thighL = BuildLeg(ctx, meshes, root, -1, pants, boots, out ai.shinL);
                ai.thighR = BuildLeg(ctx, meshes, root, 1, pants, boots, out ai.shinR);
                ai.bootL = ai.shinL.Find("Boot"); ai.bootR = ai.shinR.Find("Boot");
                ai.tool = Group(root, "HeldTool");
                if (work == World.RuralVillager.Work.ChopWood) BuildWoodcutting(ctx, root, ai, steel);
                else if (work == World.RuralVillager.Work.CarryFirewood)
                {
                    ai.tool.localPosition = new Vector3(0, 1.08f, 0.33f);
                    for (int j = 0; j < 4; j++)
                    {
                        var log = CreateMeshObject("CarriedFirewood", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, ai.tool).transform;
                        log.localPosition = new Vector3(0, j / 2 * 0.12f, (j % 2 - 0.5f) * 0.12f);
                        log.localScale = new Vector3(0.19f, 0.18f, 0.18f);
                    }
                    ctx.Exclude((p + routeB) * 0.5f, 4);
                }
                else
                {
                    BuildWateringCan(ctx, ai.tool, steel);
                    var water = new GameObject("PouringWater"); water.transform.SetParent(ai.tool, false);
                    ai.wateringStream = water.AddComponent<LineRenderer>();
                    ai.wateringStream.sharedMaterial = AccentMaterial("WateringStream", new Color(0.55f, 0.80f, 0.9f, 0.55f), ctx.Mats.Water, false);
                    ai.wateringStream.positionCount = 2; ai.wateringStream.useWorldSpace = true;
                    ai.wateringStream.startWidth = 0.012f; ai.wateringStream.endWidth = 0.025f;
                    ai.wateringStream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var patch = CreateMeshObject("TendedFlowers", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[count % 4] }, root).transform;
                    patch.localPosition = new Vector3(-0.27f, 0.02f, 0.45f); patch.localScale = Vector3.one * 0.38f;
                }
                ai.PreviewPose(work == World.RuralVillager.Work.ChopWood ? 1.65f : 0);
                ctx.Exclude(p, 1.4f);
                count++;
            }
            ctx.Count("dân làng", count);
        }

        static Transform BuildArm(BuildContext ctx, Dictionary<string, Mesh> meshes, Transform torso, float side, Material shirt, Material skin, Material glove, out Transform lower)
        {
            var upper = Group(torso, side < 0 ? "UpperArmL" : "UpperArmR"); upper.localPosition = new Vector3(side * 0.235f, 0.47f, 0);
            CreateMeshObject("Sleeve", meshes["UpperSleeve"], new[] { shirt }, upper);
            lower = Group(upper, "Elbow"); lower.localPosition = Vector3.down * 0.30f;
            CreateMeshObject("BareForearm", meshes["Forearm"], new[] { skin }, lower);
            var hand = CreateMeshObject("GlovedHand", meshes[side < 0 ? "WorkHandL" : "WorkHandR"], new[] { skin }, lower).transform;
            hand.localPosition = Vector3.down * 0.27f;
            return upper;
        }

        static Transform BuildLeg(BuildContext ctx, Dictionary<string, Mesh> meshes, Transform root, float side, Material pants, Material boots, out Transform lower)
        {
            var upper = Group(root, side < 0 ? "ThighL" : "ThighR"); upper.localPosition = new Vector3(side * 0.105f, 0.84f, 0);
            CreateMeshObject("TrousersUpper", meshes["TrouserThigh"], new[] { pants }, upper);
            lower = Group(upper, "Knee"); lower.localPosition = Vector3.down * 0.39f;
            CreateMeshObject("TrousersLower", meshes["TrouserShin"], new[] { pants }, lower);
            CreateMeshObject("Boot", meshes["LeatherBoot"], new[] { boots, ctx.Mats.AnimalDark }, lower).transform.localPosition = Vector3.down * 0.37f;
            return upper;
        }

        static void BuildWoodcutting(BuildContext ctx, Transform root, World.RuralVillager ai, Material steel)
        {
            ai.tool.localPosition = new Vector3(0.02f, 1.22f, 0.32f);
            ai.tool.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0, -0.65f, 0.76f));
            SceneryBeam(ai.tool, "AxeHandle", ctx.Mats.Wood, Vector3.down * 0.14f, Vector3.up * 0.64f, 0.021f);
            DetailBox(ai.tool, "AxeHead", steel, new Vector3(0.065f, 0.61f, 0), new Vector3(0.24f, 0.13f, 0.055f));
            float stumpDistance;
            float stumpGround = ctx.Track.TerrainHeight(root.TransformPoint(new Vector3(0, 0, 0.81f)), out stumpDistance) - root.position.y;
            ai.choppingSurfaceHeight = stumpGround + 0.89f;
            var stump = Primitive(PrimitiveType.Cylinder, "ChoppingStump", ctx.Mats.Trunk, root);
            stump.localPosition = new Vector3(0, stumpGround + 0.24f, 0.81f); stump.localScale = new Vector3(0.52f, 0.24f, 0.52f);
            var cut = Primitive(PrimitiveType.Cylinder, "StumpCutSurface", ctx.Mats.Wood, root);
            cut.localPosition = new Vector3(0, stumpGround + 0.485f, 0.81f); cut.localScale = new Vector3(0.49f, 0.008f, 0.49f);
            ai.splitLogL = Group(root, "SplitLogL"); ai.splitLogL.localPosition = new Vector3(-0.045f, stumpGround + 0.49f, 0.81f);
            ai.splitLogR = Group(root, "SplitLogR"); ai.splitLogR.localPosition = new Vector3(0.045f, stumpGround + 0.49f, 0.81f);
            foreach (var log in new[] { ai.splitLogL, ai.splitLogR })
                DetailBox(log, "SplittingBillet", ctx.Mats.Wood, new Vector3(0, 0.20f, 0), new Vector3(0.08f, 0.40f, 0.15f));
            for (int j = 0; j < 5; j++)
            {
                var log = CreateMeshObject("FirewoodStack", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, root).transform;
                log.localPosition = new Vector3(0.8f, 0.09f + j / 3 * 0.11f, 0.4f + j % 3 * 0.12f);
                log.localScale = new Vector3(0.17f, 0.17f, 0.17f);
            }
            ctx.Exclude(root.TransformPoint(new Vector3(0, 0, 0.8f)), 1);
        }

        static void BuildWateringCan(BuildContext ctx, Transform tool, Material steel)
        {
            tool.localPosition = new Vector3(0.12f, 1.03f, 0.4f);
            var body = Primitive(PrimitiveType.Cylinder, "WateringCan", steel, tool);
            body.localPosition = Vector3.down * 0.06f; body.localScale = new Vector3(0.26f, 0.13f, 0.20f);
            SceneryBeam(tool, "Spout", steel, new Vector3(-0.10f, -0.10f, 0), new Vector3(-0.38f, 0.04f, 0), 0.023f);
            SceneryBeam(tool, "HandleTop", steel, new Vector3(-0.11f, 0.16f, 0), new Vector3(0.12f, 0.16f, 0), 0.018f);
            foreach (float x in new[] { -0.11f, 0.12f }) SceneryBeam(tool, "HandleSide", steel, new Vector3(x, 0.16f, 0), new Vector3(x, 0.035f, 0), 0.018f);
        }
    }
}
