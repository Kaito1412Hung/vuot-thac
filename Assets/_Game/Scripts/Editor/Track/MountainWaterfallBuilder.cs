using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static float ApplyWaterfallLandform(RiverTrack track, Vector3 p, float natural)
        {
            foreach(var profile in track.GetComponents<MountainWaterfallLandform>())
                natural=profile.ShapeSurface(p,natural);
            foreach(var cave in track.GetComponents<ForestCaveLandform>())
                natural=cave.ShapeSurface(p,natural);
            return natural;
        }

        static void PrepareMountainWaterfall(BuildContext ctx)
        {
            var track=ctx.Track;
            var profiles=new System.Collections.Generic.List<MountainWaterfallLandform>(track.GetComponents<MountainWaterfallLandform>());
            while(profiles.Count<4)profiles.Add(track.gameObject.AddComponent<MountainWaterfallLandform>());
            foreach(var profile in profiles)profile.configured=false;
            track.RefreshWaterfallLandforms();
            if(!track.mountains)return;
            // Preserve the original waterfall; distribute three others alternately.
            float[] fractions={0.35f,0.13f,0.58f,0.82f};
            float[] sides={1,-1,-1,1};
            float[] bends={2.5f,13,-16,19};
            for(int index=0;index<4;index++)
            {
                var profile=profiles[index];
                profile.variant=index;
                profile.creekBend=bends[index];
                profile.poolRadius=index==0?13.5f:11.5f+index*0.5f;
                var sample=track.EvaluateAtDistance(track.StartDistance+track.RaceLength*fractions[index]);
                float lateral=track.terrainMargin+23;
                profile.origin=sample.center+sample.right*(lateral*sides[index]);profile.origin.y=0;
                profile.outward=sample.right*sides[index];
                profile.riverRun=lateral-track.HalfWidth+1.5f;
                profile.crestDistance=index==0?32:26+index*3;
                profile.sourceDistance=profile.crestDistance+28;
                var source=profile.World(0,0,profile.crestDistance);
                var lip=profile.World(0,0,11);
                profile.sourceHeight=MountainHeight(track,source,track.GetNearest(source))-0.8f;
                profile.lipHeight=Mathf.Min(MountainHeight(track,lip,track.GetNearest(lip))+0.1f,profile.sourceHeight-0.3f);
                profile.riverHeight=sample.WaterHeight;
                profile.poolHeight=profile.riverHeight;
                // Suối gặp nhánh sông trước khi tới sông chính thì đổ vào nhánh và dừng ở đó
                EndCreekAtFirstBranch(ctx,profile);
                profile.configured=true;
                ctx.Exclude(profile.World(0,0,-5),profile.poolRadius+14.5f);
                for(int i=0;i<=80;i++)
                {
                    float z=Mathf.Lerp(-profile.riverRun,profile.sourceDistance,i/80f);
                    ctx.Exclude(profile.World(profile.CreekCenter(z),0,z),profile.WaterWidth(z)*0.5f+6);
                }
                EditorUtility.SetDirty(profile);
            }
        }

        static void BuildWaterfall(BuildContext ctx, Transform parent)
        {
            foreach(var profile in ctx.Track.GetComponents<MountainWaterfallLandform>())
                if(profile.configured)BuildWaterfallInstance(ctx,parent,profile);
        }

        static void BuildWaterfallInstance(BuildContext ctx,Transform parent,MountainWaterfallLandform profile)
        {
            var root=Group(parent,profile.variant==0?"ForestWaterfall":"ForestWaterfall_"+profile.variant);
            root.SetPositionAndRotation(profile.origin,Quaternion.LookRotation(profile.outward));
            // Mountain meshes now exist. Add collision only to nearby chunks so
            // the water can be placed above the exact rendered rock faces.
            var mountainRoot=ctx.Track.transform.Find("Generated/Mountains");
            if(mountainRoot!=null)
                foreach(var filter in mountainRoot.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds=filter.GetComponent<Renderer>().bounds;
                    var local=profile.Local(bounds.center);
                    float radius=bounds.extents.magnitude;
                    if(Mathf.Abs(local.x)<radius+20 && local.z> -radius-15 && local.z<profile.sourceDistance+radius+5)
                        if(filter.GetComponent<MeshCollider>()==null)filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                }
            Physics.SyncTransforms();
            var falling=FlowMaterial("MountainWaterfall",new Color(0.18f,0.52f,0.68f,0.96f),0.65f,0.80f);
            var creek=FlowMaterial("MountainCreek",new Color(0.18f,0.52f,0.68f,0.94f),0.24f,0.12f);
            falling.SetFloat("_Cascade",1);
            EditorUtility.SetDirty(falling);
            creek.SetFloat("_MouthFade",1);
            EditorUtility.SetDirty(creek);
            var headwater=FlowMaterial("MountainHeadwater",new Color(0.18f,0.52f,0.68f,0.94f),0.13f,0.08f);
            headwater.SetFloat("_Cascade",0);headwater.SetFloat("_MouthFade",0);
            EditorUtility.SetDirty(headwater);
            AddMountainWater(ctx,root,profile,"MountainHeadwaterBasin",profile.sourceDistance,profile.crestDistance,80,headwater,true);
            AddMountainWater(ctx,root,profile,"MountainSpringAndFalls",profile.crestDistance,-4,100,falling,true);
            AddMountainWater(ctx,root,profile,"CreekToMainRiver",-4,-profile.riverRun+0.5f,130,creek,false);
            BuildWaterfallHead(ctx,root,profile);
            BuildCircularPlungePool(ctx,root,profile);
            // The source emerges from a small crevice in the existing mountain;
            // these are embedded ledge stones, not a separate stone tower.
            for(int i=0;i<9;i++)
            {
                float z=Mathf.Lerp(-3,profile.sourceDistance,i/8f)+Mathf.Sin(i*7.3f)*1.2f;
                if(z>20)continue;
                foreach(float side in new[]{-1f,1f})
                {
                    if ((i+(side>0?1:0))%3==0) continue;
                    float x=profile.CreekCenter(z)+side*(profile.WaterWidth(z)*0.5f+1.2f);
                    var p=profile.World(x,0,z);p.y=RenderedGround(ctx,profile,p)-0.5f;
                    var rock=CreateMeshObject("EmbeddedCliffLedge",ctx.Props.Rocks[i%4],new[]{ctx.Mats.MountainRock},root).transform;
                    rock.position=p;
                    rock.localScale=new Vector3(1.5f+(i%3)*0.35f,1.2f,2.1f);
                    rock.localRotation=Quaternion.Euler(0,i*43,0);
                }
            }
            for(int i=1;i<32;i++)
                foreach(float side in new[]{-1f,1f})
                {
                    float z=Mathf.Lerp(-7,-profile.riverRun,i/32f);
                    float x=profile.CreekCenter(z)+side*(profile.WaterWidth(z)*0.5f+0.3f);
                    var p=profile.World(x,0,z);p.y=RenderedGround(ctx,profile,p)-0.25f;
                    var rock=CreateMeshObject("CreekBankStone",ctx.Props.Rocks[i%4],new[]{ctx.Mats.Rock},root).transform;
                    rock.position=p;rock.localScale=new Vector3(0.65f,0.55f,0.85f);rock.localRotation=Quaternion.Euler(0,i*37,0);
                }
            Spray(root,"MountainImpactMist",new Vector3(0,profile.poolHeight+0.25f,1.5f),false);
            Spray(root,"MountainSplash",new Vector3(0,profile.poolHeight+0.20f,1.5f),true);
            ctx.Count("thác từ núi",1);
        }

        static float RenderedGround(BuildContext ctx, MountainWaterfallLandform profile,Vector3 world)
        {
            float expected = world.y;
            var near=ctx.Track.GetNearest(world);
            expected=near.horizontalDistance>=ctx.Track.terrainMargin-MountainOverlap ? MountainHeight(ctx.Track,world,near) : ctx.Track.TerrainHeightAt(world,near.horizontalDistance,near.WaterHeight);
            if(Physics.Raycast(new Vector3(world.x,expected+60,world.z),Vector3.down,out var hit,120,~0,QueryTriggerInteraction.Ignore))return hit.point.y;
            return expected;
        }

        static void BuildWaterfallHead(BuildContext ctx,Transform root,MountainWaterfallLandform profile)
        {
            // Crest spray sits where the upstream basin spills over the rock lip.
            foreach(float z in new[]{profile.crestDistance+0.4f,11f})
            {
                var p=profile.World(0,0,z);
                float y=Mathf.Max(profile.WaterLevel(z),RenderedGround(ctx,profile,p))+0.22f;
                string name=z>20?"SpringWhitewater":"CrestDroplets";
                Spray(root,name,new Vector3(0,y,z),true);
                var ps=root.Find(name).GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.startLifetime=new ParticleSystem.MinMaxCurve(0.45f,1.1f);
                main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.09f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(0.4f,1.2f);main.gravityModifier=0.65f;
                main.maxParticles=200;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(profile.WaterWidth(z)*0.8f,0.15f,0.25f);
                ps.transform.localRotation=Quaternion.identity;
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
                velocity.x=new ParticleSystem.MinMaxCurve(-0.2f,0.2f);
                velocity.y=new ParticleSystem.MinMaxCurve(-2.0f,-1.6f);
                velocity.z=new ParticleSystem.MinMaxCurve(-2.9f,-2.3f);
                var emission=ps.emission;emission.rateOverTime=95;
                ps.Simulate(2,true,true);ps.Play();
            }
        }

        static void BuildCircularPlungePool(BuildContext ctx,Transform root,MountainWaterfallLandform profile)
        {
            var basin=Group(root,"CircularPlungePool");
            // Leave the upstream fall and downstream outlet open.
            for(int i=0;i<34;i++)
            {
                float angle=i*Mathf.PI*2/34;
                float forward=Mathf.Cos(angle);
                if(forward>0.52f || forward< -0.94f)continue;
                float radius=profile.poolRadius+0.6f+Mathf.Sin(i*4.7f)*0.35f;
                var p=profile.World(Mathf.Sin(angle)*radius,0,-5+forward*radius);
                p.y=RenderedGround(ctx,profile,p)-0.45f;
                var rock=CreateMeshObject("PoolRimBoulder",ctx.Props.Rocks[i%4],new[]{ctx.Mats.MountainRock},basin).transform;
                rock.position=p;rock.localScale=new Vector3(1.5f+(i%3)*0.25f,1.1f+(i%2)*0.3f,1.6f);
                rock.localRotation=Quaternion.Euler(i%3*9,i*53,0);
            }
            // A low solid outcrop with a flat crown and rounded shoulders.
            const int segments=64;
            float[] radii={0,1.0f,2.3f,2.95f,3.3f,3.15f,2.35f,0};
            float[] levels={1.05f,1.05f,1.04f,0.95f,0.55f,-0.15f,-0.7f,-0.7f};
            var stone=new G();
            for(int r=0;r<radii.Length;r++)
                for(int j=0;j<=segments;j++)
                {
                    float angle=j*Mathf.PI*2/segments;
                    float irregular=1+0.055f*Mathf.Sin(angle*(3+profile.variant)+0.4f)+0.035f*Mathf.Cos(angle*7)+0.018f*Mathf.Sin(angle*13);
                    float edgeWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.8f,3.0f,radii[r]));
                    float roughness=edgeWeight*(0.10f*Mathf.Sin(angle*5+r*0.7f)+0.07f*Mathf.Cos(angle*11-r*0.5f));
                    float xShape=profile.variant==1?1.18f:1;
                    float zShape=profile.variant==2?1.2f:1;
                    var v=new Vector3(Mathf.Sin(angle)*radii[r]*irregular*xShape,levels[r]+roughness,Mathf.Cos(angle)*radii[r]*irregular*zShape);
                    stone.vertices.Add(v);stone.normals.Add(Vector3.up);stone.uv.Add(new Vector2(v.x,v.z)*0.5f);
                }
            for(int r=0;r<radii.Length-1;r++)
                for(int j=0;j<segments;j++)
                {
                    int i=r*(segments+1)+j,k=i+segments+1;
                    stone.triangles[0].AddRange(new[]{i,k,i+1,i+1,k,k+1});
                }
            var stoneMesh=FinishGeometry(stone,"FlatRoundedOutcrop",1);stoneMesh.RecalculateNormals();ctx.Meshes.Add(stoneMesh);
            var outcrop=CreateMeshObject("CentralFlatRoundedRock",stoneMesh,new[]{ctx.Mats.Rock},basin).transform;
            float islandZ=-7;
            float islandX=profile.CreekCenter(islandZ);
            outcrop.position=profile.World(islandX,profile.poolHeight,islandZ);
            outcrop.gameObject.AddComponent<MeshCollider>().sharedMesh=stoneMesh;
            if(profile.variant==3)
            {
                for(int flower=0;flower<22;flower++)
                {
                    float angle=flower*2.39996f;
                    float radius=0.35f+Mathf.Sqrt(flower/22f)*2.0f;
                    var plant=CreateMeshObject("IslandWildflowers_"+flower,ctx.Props["Flowers"],new[]{ctx.Mats.Leaves,ctx.Mats.Flowers[flower%ctx.Mats.Flowers.Length]},basin).transform;
                    plant.position=profile.World(islandX+Mathf.Sin(angle)*radius,profile.poolHeight+1.03f,islandZ+Mathf.Cos(angle)*radius);
                    plant.localScale=Vector3.one*(0.7f+(flower%4)*0.12f);
                    plant.localRotation=Quaternion.Euler(0,flower*37,0);
                }
            }
            else
            {
                int treeCount=profile.variant==2?3:1;
                for(int treeIndex=0;treeIndex<treeCount;treeIndex++)
                {
                    var kind=profile.variant==1?PropLibrary.TreeKind.Pine:PropLibrary.TreeKind.Round;
                    var treeMesh=ctx.Props.Tree(kind);
                    var tree=CreateMeshObject("IslandTree_"+treeIndex,treeMesh,new[]{ctx.Mats.Trunk,ctx.Mats.Leaves},basin).transform;
                    float height=treeCount==1?(profile.variant==0?4.2f:3.7f):2.5f+treeIndex*0.35f;
                    float scale=height/treeMesh.bounds.size.y;
                    float offsetX=treeCount==1?0:Mathf.Sin(treeIndex*2.094f)*1.45f;
                    float offsetZ=treeCount==1?0:Mathf.Cos(treeIndex*2.094f)*1.25f;
                    tree.localScale=Vector3.one*scale;
                    tree.position=profile.World(islandX+offsetX,profile.poolHeight+1.02f-treeMesh.bounds.min.y*scale,islandZ+offsetZ);
                }
            }
            Spray(root,"PoolImpactFoam",new Vector3(0,profile.poolHeight+0.15f,-1),true);
        }

        static void AddMountainWater(BuildContext ctx,Transform root,MountainWaterfallLandform profile,string name,float start,float end,int rows,Material material,bool cascade)
        {
            const int columns=16;
            var g=new G();
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows,z=Mathf.Lerp(start,end,t);
                float center=profile.CreekCenter(z),width=profile.WaterWidth(z);
                if(cascade && z<=3)width=Mathf.Lerp(7,width,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,-3,z)));
                if(cascade)width*=0.94f+0.045f*Mathf.Sin(z*2.7f)+0.025f*Mathf.Sin(z*5.1f);
                // Use a common row level so water stays a surface, not a rippled carpet.
                float height=profile.WaterLevel(z)+0.10f;
                for(int check=0;cascade && z>3 && z<profile.crestDistance-4 && check<=4;check++)
                {
                    var p=profile.World(center+(check/4f-0.5f)*width,0,z);
                    height=Mathf.Max(height,RenderedGround(ctx,profile,p)+0.09f);
                }
                for(int col=0;col<=columns;col++)
                {
                    float u=col/(float)columns;
                    float edge=Mathf.Sin(u*Mathf.PI);
                    float x=center+(u-0.5f)*width;
                    float wave=cascade ? Mathf.Sin(u*20+z*0.7f)*0.025f*edge : 0;
                    var wp=profile.World(x,height+wave,z);
                    if (!cascade)
                    {
                        // Trim to the curved river bank rather than leaving a
                        // rectangular overlay protruding across the race water.
                        for(int trim=0;trim<2;trim++)
                        {
                            var nearest=ctx.Track.GetNearest(wp);
                            float riverEdge=ctx.Track.HalfWidth+1.5f;
                            if(nearest.horizontalDistance>=riverEdge)break;
                            wp+=profile.outward*(riverEdge-nearest.horizontalDistance);
                        }
                        // Match the blue river exactly across the complete mouth,
                        // including its slight longitudinal slope.
                        wp.y = NearestWaterHeight(ctx, wp);
                    }
                    else if(z<3)
                    {
                        float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,-4,z));
                        wp.y=Mathf.Lerp(wp.y,ctx.Track.GetNearest(wp).WaterHeight,settle);
                    }
                    g.vertices.Add(root.InverseTransformPoint(wp));g.normals.Add(Vector3.up);g.uv.Add(new Vector2(u,t*(cascade?5:8)));
                }
            }
            for(int row=0;row<rows;row++)
                for(int col=0;col<columns;col++)
                {
                    int a=row*(columns+1)+col,b=a+columns+1;
                    g.triangles[0].AddRange(new[]{a,b,b+1,a,b+1,a+1});
                }
            var mesh=FinishGeometry(g,name,1);ctx.Meshes.Add(mesh);
            mesh.RecalculateNormals();
            CreateMeshObject(name,mesh,new[]{material},root).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}
