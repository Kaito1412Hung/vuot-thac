using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        const string HumanFolder = "Assets/ThirdParty/People/Grab3D/";

        static void BuildRealisticVillagers(BuildContext ctx, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HumanFolder + "Source/man-base-character-rigged.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>(HumanFolder + "RealisticMan.mat");
            var womanSource = AssetDatabase.LoadAssetAtPath<GameObject>(HumanFolder + "Woman/Source/woman-base-character-rigged.fbx");
            var womanMaterial = AssetDatabase.LoadAssetAtPath<Material>(HumanFolder + "Woman/RealisticWoman.mat");
            var maleTemplate = VillagerAppearance.Prepare(source, "Male");
            var femaleTemplate = VillagerAppearance.Prepare(womanSource, "Female");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanFolder + "VillagerLocomotion.controller");
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(HumanFolder + "VillagerLocomotion.controller");
                var state = controller.layers[0].stateMachine.AddState("Walk");
                state.motion = AssetDatabase.LoadAllAssetsAtPath(HumanFolder + "Source/man-base-character-rigged.fbx").OfType<AnimationClip>().First(c => c.name == "walk");
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }
            var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers; EditorUtility.SetDirty(controller);
            var hats = VillagerMeshes.Load();
            var straw = VillagerMaterial(ctx, "FarmerStrawHat", new Color(0.78f, 0.62f, 0.34f), true);
            var steel = VillagerMaterial(ctx, "ToolSteel", new Color(0.43f, 0.49f, 0.50f));
            var leather = VillagerMaterial(ctx, "FarmerWorkBootLeather", new Color(0.18f, 0.11f, 0.065f));
            var sole = VillagerMaterial(ctx, "FarmerBootRubber", new Color(0.055f, 0.06f, 0.055f));
            var laces = VillagerMaterial(ctx, "FarmerBootLaces", new Color(0.55f, 0.48f, 0.35f));
            Color[] colors = { new Color(0.25f,0.36f,0.22f), new Color(0.47f,0.25f,0.17f), new Color(0.20f,0.29f,0.40f), new Color(0.67f,0.59f,0.42f), new Color(0.35f,0.24f,0.16f), new Color(0.20f,0.40f,0.38f) };
            var outfits = colors.Select((color,index) => VillagerMaterial(ctx, "VillagerWorkVest_" + index, color, true)).ToArray();
            string[] names = { "An", "Binh", "Lam", "Hoa", "Phuc", "Lan", "Minh", "Mai", "Duc", "Hanh", "Son", "Thao" };
            var homes = ctx.Track.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && (m.sharedMesh.name == "Cabin" || m.sharedMesh.name == "HouseTall")).ToArray();
            int count = 0;
            for (int i = 0; i < homes.Length && count < 12; i += 2)
            {
                var house = homes[i].transform;
                var work = (World.RuralVillager.Work)(count % 3);
                Vector3 p = house.TransformPoint(work == World.RuralVillager.Work.ChopWood ? new Vector3(5, 0, -2.5f)
                    : work == World.RuralVillager.Work.CarryFirewood ? new Vector3(-3, 0, -6.5f) : new Vector3(-4.5f, 0, -6.5f));
                float d; p.y = ctx.Track.TerrainHeight(p, out d);
                if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 1.5f) continue;
                Vector3 end = house.TransformPoint(new Vector3(3, 0, -6.5f));
                end.y = ctx.Track.TerrainHeight(end, out d);
                bool routeSafe = d >= ctx.Track.HalfWidth + ctx.Track.bankWidth + 1.5f && Mathf.Abs(end.y - p.y) < 1.5f;
                if (work == World.RuralVillager.Work.CarryFirewood)
                {
                    for (int sample = 0; sample <= 12; sample++)
                    {
                        ctx.Track.TerrainHeight(Vector3.Lerp(p, end, sample / 12f), out d);
                        if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 1.2f) routeSafe = false;
                    }
                    if (!routeSafe) continue;
                }
                var root = Group(parent, "Farmer_" + count.ToString("00") + "_" + work);
                root.SetPositionAndRotation(p, house.rotation * Quaternion.Euler(0, 180, 0));
                bool woman = count % 2 == 1;
                var template = woman ? femaleTemplate : maleTemplate;
                float bottom = template.bottom, top = template.top;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(woman ? womanSource : source);
                model.name = "RiggedHuman"; model.transform.SetParent(root, false);
                model.transform.localPosition = Vector3.down * bottom;
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.sharedMaterial = woman ? womanMaterial : material; r.updateWhenOffscreen = true; }
                var actor = root.gameObject.AddComponent<World.RealisticVillager>();
                actor.displayName = names[count]; actor.isWoman = woman;
                actor.work = work; actor.model = model.transform; actor.animator = model.GetComponent<Animator>();
                actor.animator.runtimeAnimatorController = controller; actor.animator.applyRootMotion = false;
                actor.animator.enabled = false;
                VillagerAppearance.Apply(template, model, actor.animator, count / 2, outfits[(count / 2 + (woman ? 2 : 0)) % outfits.Length], leather, sole, laces);
                model.AddComponent<World.VillagerAnimatorIK>().owner = actor;
                actor.routeA = p; actor.routeB = end; actor.phase = count * 0.69f;
                var head = actor.animator.GetBoneTransform(HumanBodyBones.Head);
                var hat = CreateMeshObject("FarmerHat", hats["WovenConicalHat"], new[] { straw }, root).transform;
                hat.localPosition = new Vector3(0, top - bottom - 0.275f - 0.08f, 0);
                hat.SetParent(head, true);
                actor.CaptureRig();
                actor.tool = Group(root, "HeldTool");
                if (work == World.RuralVillager.Work.ChopWood) RealisticWoodcutting(ctx, root, actor, steel);
                else if (work == World.RuralVillager.Work.CarryFirewood)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var log = CreateMeshObject("CarriedFirewood", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, actor.tool).transform;
                        log.localPosition = new Vector3(0, j / 2 * 0.11f, (j % 2 - 0.5f) * 0.12f);
                        log.localScale = new Vector3(0.18f, 0.17f, 0.17f);
                    }
                    ctx.Exclude((p + end) * 0.5f, 4);
                }
                else
                {
                    BuildWateringCan(ctx, actor.tool, steel);
                    var water = new GameObject("PouringWater"); water.transform.SetParent(actor.tool, false);
                    actor.wateringStream = water.AddComponent<LineRenderer>();
                    actor.wateringStream.sharedMaterial = AccentMaterial("WateringStream", new Color(0.55f, 0.80f, 0.9f, 0.55f), ctx.Mats.Water, false);
                    actor.wateringStream.positionCount = 2; actor.wateringStream.useWorldSpace = true;
                    actor.wateringStream.startWidth = 0.012f; actor.wateringStream.endWidth = 0.025f;
                    var flowers = CreateMeshObject("TendedFlowers", ctx.Props["Flowers"], new[] { ctx.Mats.Leaves, ctx.Mats.Flowers[count % 4] }, root).transform;
                    flowers.localPosition = new Vector3(-0.27f, 0.02f, 0.45f); flowers.localScale = Vector3.one * 0.38f;
                }
                actor.PreviewPose(work == World.RuralVillager.Work.ChopWood ? 1.65f : 0);
                var lod = root.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.008f, root.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<LODGroup>() == lod).ToArray()) }); lod.RecalculateBounds();
                ctx.Exclude(p, 1.4f); count++;
            }
            ctx.Count("dĂ¢n lĂ ng Humanoid", count);
        }

        static void RealisticWoodcutting(BuildContext ctx, Transform root, World.RealisticVillager actor, Material steel)
        {
            SceneryBeam(actor.tool, "AxeHandle", ctx.Mats.Wood, Vector3.down * 0.14f, Vector3.up * 0.64f, 0.021f);
            DetailBox(actor.tool, "AxeHead", steel, new Vector3(0.065f, 0.61f, 0), new Vector3(0.24f, 0.13f, 0.055f));
            float d;
            float ground = ctx.Track.TerrainHeight(root.TransformPoint(new Vector3(0, 0, 0.81f)), out d) - root.position.y;
            actor.choppingSurfaceHeight = ground + 0.89f;
            var stump = Primitive(PrimitiveType.Cylinder, "ChoppingStump", ctx.Mats.Trunk, root);
            stump.localPosition = new Vector3(0, ground + 0.24f, 0.81f); stump.localScale = new Vector3(0.52f, 0.24f, 0.52f);
            var cut = Primitive(PrimitiveType.Cylinder, "StumpCutSurface", ctx.Mats.Wood, root);
            cut.localPosition = new Vector3(0, ground + 0.485f, 0.81f); cut.localScale = new Vector3(0.49f, 0.008f, 0.49f);
            actor.splitLogL = Group(root, "SplitLogL"); actor.splitLogL.localPosition = new Vector3(-0.045f, ground + 0.49f, 0.81f);
            actor.splitLogR = Group(root, "SplitLogR"); actor.splitLogR.localPosition = new Vector3(0.045f, ground + 0.49f, 0.81f);
            foreach (var log in new[] { actor.splitLogL, actor.splitLogR }) DetailBox(log, "Billet", ctx.Mats.Wood, new Vector3(0, 0.2f, 0), new Vector3(0.08f, 0.4f, 0.15f));
            for (int j = 0; j < 5; j++)
            {
                var log = CreateMeshObject("FirewoodStack", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, root).transform;
                log.localPosition = new Vector3(0.8f, 0.09f + j / 3 * 0.11f, 0.4f + j % 3 * 0.12f); log.localScale = Vector3.one * 0.17f;
            }
            ctx.Exclude(root.TransformPoint(new Vector3(0, 0, 0.81f)), 1);
        }
    }
}
