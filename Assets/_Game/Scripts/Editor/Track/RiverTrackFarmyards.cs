using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static void BuildFarmyards(BuildContext ctx, Transform parent)
        {
            var hen = FarmHenMesh.Load();
            var feathers = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/Animals/Chicken/ChickenFeathers.mat");
            int built = 0;
            for (int site = 0; site < 3; site++)
            {
                Vector3 p = Vector3.zero; Quaternion facing = Quaternion.identity; bool found = false;
                for (int attempt = 0; attempt < 36 && !found; attempt++)
                {
                    float along = ctx.Track.StartDistance + ctx.Track.RaceLength * (0.18f + site * 0.30f) + (attempt / 2 - 8) * 4;
                    var s = ctx.Track.EvaluateAtDistance(along); float side = attempt % 2 == 0 ? 1 : -1;
                    p = s.center + s.right * side * (ctx.Track.HalfWidth + ctx.Track.bankWidth + 27 + attempt % 3 * 5);
                    float d; float ground = ctx.Track.TerrainHeight(p,out d);
                    if (d < ctx.Track.HalfWidth + ctx.Track.bankWidth + 18 || ctx.IsExcluded(p,8)) continue;
                    if (MaxTerrain(ctx.Track,p,6) - ground > 0.9f) continue;
                    p.y = ground; facing = Quaternion.LookRotation(s.right * side); found = true;
                }
                if (!found) continue;
                var yard = Group(parent,site == 0 ? "ChickenFarm" : site == 1 ? "RabbitFarm" : "DuckFarm");
                yard.SetPositionAndRotation(p,facing);
                FarmFence(ctx,yard,new Vector3(-6,0,-4),new Vector3(-1,0,-4));
                FarmFence(ctx,yard,new Vector3(1,0,-4),new Vector3(6,0,-4));
                FarmFence(ctx,yard,new Vector3(-6,0,4),new Vector3(6,0,4));
                FarmFence(ctx,yard,new Vector3(-6,0,-4),new Vector3(-6,0,4));
                FarmFence(ctx,yard,new Vector3(6,0,-4),new Vector3(6,0,4));
                // Gate remains shut while animals roam within safe inner bounds.
                FarmFence(ctx,yard,new Vector3(-1,0,-4),new Vector3(1,0,-4));
                var shelter = CreateMeshObject("AnimalShelter",ctx.Props["Cabin"],new[]{ctx.Mats.Wood,ctx.Mats.WoodDark,ctx.Mats.Roofs[site],ctx.Mats.Rock},yard).transform;
                shelter.localPosition = YardGround(ctx,yard,new Vector3(0,0,2.3f)) + Vector3.up*0.15f;
                shelter.localScale = new Vector3(0.65f,0.45f,0.48f);
                var feed = Group(yard,"FeedTrough"); feed.localPosition = YardGround(ctx,yard,new Vector3(3,0,-1.8f));
                DetailBox(feed,"Base",ctx.Mats.Wood,new Vector3(0,0.12f,0),new Vector3(1.6f,0.12f,0.55f));
                foreach(float z in new[]{-0.28f,0.28f}) DetailBox(feed,"Rim",ctx.Mats.WoodDark,new Vector3(0,0.26f,z),new Vector3(1.7f,0.24f,0.07f));
                foreach(float x in new[]{-0.82f,0.82f}) DetailBox(feed,"End",ctx.Mats.WoodDark,new Vector3(x,0.26f,0),new Vector3(0.07f,0.24f,0.6f));
                DetailBox(feed,"AnimalFeed",ctx.Mats.Hay,new Vector3(0,0.21f,0),new Vector3(1.5f,0.10f,0.45f));
                float water = 0;
                if (site == 2)
                {
                    Vector3 center = yard.TransformPoint(new Vector3(-1,0,-0.6f)); water = MaxTerrain(ctx.Track,center,2.5f)+0.12f;
                    var basin = Primitive(PrimitiveType.Cylinder,"PondBasin",ctx.Mats.Rock,yard);
                    basin.localPosition = new Vector3(-1,water-yard.position.y-0.15f,-0.6f); basin.localScale=new Vector3(5.2f,0.15f,4.2f);
                    var pond = Primitive(PrimitiveType.Cylinder,"DuckPond",ctx.Mats.Water,yard);
                    pond.localPosition = new Vector3(-1,water-yard.position.y,-0.6f); pond.localScale=new Vector3(4.9f,0.025f,3.9f);
                }
                int n = site == 0 ? 4 : site == 1 ? 6 : 5;
                for(int animal=0;animal<n;animal++)
                {
                    var root=Group(yard,site==0 ? "Hen_"+animal : site==1 ? "PetRabbit_"+animal : "DomesticDuck_"+animal);
                    Vector3 home=site==2 ? new Vector3(-1+(animal%3-1)*0.65f,water-yard.position.y,-0.6f+(animal/3-0.5f)*0.6f) : YardGround(ctx,yard,new Vector3((animal%3-1)*1.5f,0,-1.2f+animal/3*1.5f));
                    root.localPosition=home; root.localRotation=Quaternion.Euler(0,animal*73,0);
                    if(site==0)
                    {
                        var body=Group(root,"BodyBone"); var neck=Group(body,"NeckBone"); neck.localPosition=hen.bindposes[1].inverse.GetColumn(3);
                        var skin=root.gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh=hen; skin.sharedMaterial=feathers; skin.bones=new[]{body,neck};skin.rootBone=body;
                        skin.localBounds=hen.bounds;skin.updateWhenOffscreen=true;
                        var idle=root.gameObject.AddComponent<World.FarmHen>(); idle.neck=neck;idle.phase=animal*1.1f;
                    }
                    else
                    {
                        var body=CreateMeshObject("AnimalBody",ctx.Props[site==1 ? "Rabbit" : "Duck"],site==1 ? new[]{ctx.Mats.RabbitGray,ctx.Mats.AnimalCream,ctx.Mats.AnimalDark} : new[]{ctx.Mats.DuckBrown,ctx.Mats.DuckGreen,ctx.Mats.Beak,ctx.Mats.AnimalDark},root).transform;
                        var ai=root.gameObject.AddComponent<World.AnimalWander>();ai.body=body;ai.gait=site==1 ? World.AnimalWander.Gait.Hop : World.AnimalWander.Gait.Swim;
                        ai.walkSpeed=site==1 ? 0.5f : 0.22f;ai.fleeDistance=0;ai.wanderRadius=site==1 ? 0.65f : 0.35f;ai.waterHeight=water;
                    }
                    var lod=root.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(0.01f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                }
                ctx.Exclude(p,8); built++;
            }
            ctx.Count("khu nuôi vật",built);
        }

        static Vector3 YardGround(BuildContext ctx,Transform yard,Vector3 local)
        {
            Vector3 world=yard.TransformPoint(local);float d;world.y=ctx.Track.TerrainHeight(world,out d);return yard.InverseTransformPoint(world);
        }

        static void FarmFence(BuildContext ctx,Transform yard,Vector3 a,Vector3 b)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/2);
            for(int i=0;i<count;i++)
            {
                var p=YardGround(ctx,yard,Vector3.Lerp(a,b,i/(float)count));var q=YardGround(ctx,yard,Vector3.Lerp(a,b,(i+1)/(float)count));
                SceneryBeam(yard,"FencePost",ctx.Mats.WoodDark,p-Vector3.up*0.15f,p+Vector3.up*1.05f,0.055f);
                foreach(float h in new[]{0.35f,0.80f})SceneryBeam(yard,"FenceRail",ctx.Mats.Wood,p+Vector3.up*h,q+Vector3.up*h,0.035f);
            }
        }
    }
}
