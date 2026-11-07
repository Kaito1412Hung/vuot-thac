using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    public static partial class RiverTrackBuilder
    {
        static Vector3 caveOrigin,caveForward,caveRight;
        static Quaternion caveRotation;
        static int caveVariant;
        static float caveBend;
        static Vector3 CaveWorld(Vector3 p) => caveOrigin+caveRotation*p;
        static Vector3 CaveLocal(Vector3 p) => Quaternion.Inverse(caveRotation)*(p-caveOrigin);

        static void PrepareForestCave(BuildContext ctx)
        {
            var t=ctx.Track;
            var caves=new List<VuotThac.Track.ForestCaveLandform>(t.GetComponents<VuotThac.Track.ForestCaveLandform>());
            while(caves.Count<4)caves.Add(t.gameObject.AddComponent<VuotThac.Track.ForestCaveLandform>());
            foreach(var cave in caves)cave.configured=false;
            float[] fractions={0.42f,0.21f,0.68f,0.93f};
            float[] sides={1,1,-1,-1};
            float[] bends={4.5f,-3.8f,5.5f,-5.0f};
            for(int i=0;i<4;i++)
            {
                var cave=caves[i];var sample=t.EvaluateAtDistance(t.StartDistance+t.RaceLength*fractions[i]);
                cave.variant=i;cave.bend=bends[i];cave.forward=sample.right*sides[i];
                cave.origin=sample.center+cave.forward*(t.terrainMargin+(i==0?0:9));
                cave.origin.y=Mathf.Min(MountainHeight(t,cave.origin,t.GetNearest(cave.origin))-0.8f,sample.WaterHeight+10);
                cave.configured=true;EditorUtility.SetDirty(cave);ctx.Exclude(cave.origin,22);
            }
        }

        static void BuildForestCave(BuildContext ctx,Transform parent)
        {
            if(!ctx.Track.mountains)return;
            foreach(var cave in ctx.Track.GetComponents<VuotThac.Track.ForestCaveLandform>())
            {
                if(!cave.configured)continue;
                var root=Group(parent,"Cave_"+cave.variant);
                caveOrigin=cave.origin;caveForward=cave.forward;caveRight=Vector3.Cross(Vector3.up,caveForward);
                caveRotation=Quaternion.LookRotation(caveForward);caveVariant=cave.variant;caveBend=cave.bend;
                BuildForestCaveInstance(ctx,root);
            }
        }

        static void BuildForestCaveInstance(BuildContext ctx,Transform parent)
        {
            parent.SetPositionAndRotation(caveOrigin,caveRotation);
            CutCaveOpening(ctx);
            var rock=ctx.Mats.MountainRock;
            var shadow=AccentMaterial("CaveShadowStone",new Color(0.105f,0.12f,0.115f),ctx.Mats.Rock,false);
            var deep=AccentMaterial("CaveDeepStone",new Color(0.025f,0.032f,0.029f),ctx.Mats.Rock,false);
            var ground=AccentMaterial("CaveGravel",new Color(0.26f,0.25f,0.21f),ctx.Mats.Sand,false);
            // A continuous rock arch frames an actual cut in the mountain mesh.
            const int archSegments=20;
            var arch=new FlatMeshBuilder(1);
            Vector3 Edge(int segment,bool outer,float z)
            {
                float angle=segment*Mathf.PI/archSegments;
                float noise=outer?1+0.045f*Mathf.Sin(segment*3.7f+caveVariant*1.3f):1+0.035f*Mathf.Sin(segment*2.3f+caveVariant*0.8f);
                return new Vector3(Mathf.Cos(angle)*(outer?7.8f:4.7f)*noise,0.15f+Mathf.Sin(angle)*(outer?10.3f:7.0f)*noise,z);
            }
            for(int j=0;j<archSegments;j++)
            {
                var a=Edge(j,false,-0.7f);var b=Edge(j+1,false,-0.7f);
                var c=Edge(j,true,-0.1f);var d=Edge(j+1,true,-0.1f);
                arch.TriOutward(0,a,b,c,new Vector3(0,4,20));arch.TriOutward(0,b,d,c,new Vector3(0,4,20));
                var e=Edge(j,false,3.5f);var f=Edge(j+1,false,3.5f);
                var outside=(a+b)*0.5f;outside.y+=3;outside.x*=4;
                arch.TriOutward(0,a,e,b,outside);arch.TriOutward(0,b,e,f,outside);
                var outerBackA=Edge(j,true,8);var outerBackB=Edge(j+1,true,8);
                arch.TriOutward(0,c,d,outerBackA,new Vector3(0,3,4));
                arch.TriOutward(0,d,outerBackB,outerBackA,new Vector3(0,3,4));
            }
            SaveCaveMesh(ctx,parent,arch,"NaturalRockArch",new[]{rock});
            // The passage turns slightly so the far wall is not visible from outside.
            const int slices=12,around=20;
            var tunnel=new FlatMeshBuilder(3);
            Vector3 Tunnel(int row,int j)
            {
                float z=row*2f;
                float angle=j*Mathf.PI/around;
                float irregular=1+0.035f*Mathf.Sin(row*2.7f+j*1.9f);
                float xCenter=Mathf.SmoothStep(0,1,z/24)*caveBend;
                return new Vector3(xCenter+Mathf.Cos(angle)*Mathf.Lerp(4.7f,3.2f,z/24)*irregular,
                    -0.2f+Mathf.Sin(angle)*Mathf.Lerp(7.35f,5.95f,z/24)*irregular,z);
            }
            for(int row=0;row<slices;row++)
            {
                int material=row<2?0:row<6?1:2;
                for(int j=0;j<around;j++)
                {
                    var a=Tunnel(row,j);var b=Tunnel(row+1,j);var c=Tunnel(row,j+1);var d=Tunnel(row+1,j+1);
                    var center=new Vector3(Mathf.SmoothStep(0,1,(row+0.5f)*2/24)*caveBend,2,row*2+1);
                    var outside=(a+b+c+d)*0.25f;outside=center+(outside-center)*10;
                    tunnel.TriOutward(material,a,b,c,outside);tunnel.TriOutward(material,c,b,d,outside);
                }
            }
            for(int j=0;j<around;j++)tunnel.TriOutward(2,Tunnel(slices,j),Tunnel(slices,j+1),new Vector3(caveBend,0,24),new Vector3(caveBend,2,40));
            SaveCaveMesh(ctx,parent,tunnel,"CurvedCaveInterior",new[]{rock,shadow,deep});
            var floor=new FlatMeshBuilder(2);
            for(int row=0;row<slices;row++)
            {
                var a=Tunnel(row,0);var b=Tunnel(row,around);var c=Tunnel(row+1,0);var d=Tunnel(row+1,around);
                a.y=b.y=c.y=d.y=0.08f;
                floor.TriUp(row<4?0:1,a,b,c);floor.TriUp(row<4?0:1,b,d,c);
            }
            SaveCaveMesh(ctx,parent,floor,"WalkableCaveFloor",new[]{ground,deep});
            var apron=new FlatMeshBuilder(1);
            Vector3 Apron(int x,int z)
            {
                var local=new Vector3(x,0,z);
                var world=CaveWorld(local);
                float natural=RenderedCaveGround(ctx,world)-caveOrigin.y;
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-6,-1.5f,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.5f,9,Mathf.Abs(x))));
                local.y=Mathf.Lerp(natural+0.08f,0.08f,blend);
                return local;
            }
            for(int z=-6;z<2;z++)for(int x=-9;x<9;x++)
            {
                var a=Apron(x,z);var b=Apron(x+1,z);var c=Apron(x,z+1);var d=Apron(x+1,z+1);
                apron.TriUp(0,a,c,b);apron.TriUp(0,b,c,d);
            }
            SaveCaveMesh(ctx,parent,apron,"GrassyCaveEntrance",new[]{ctx.Mats.Grass});
            // Broken embedded boulders help merge the arch into the existing cliff.
            for(int i=0;i<15;i++)
            {
                float angle=i*Mathf.PI/14;
                var p=new Vector3(Mathf.Cos(angle)*(6.1f+(i%3)*0.2f),Mathf.Sin(angle)*8.2f-0.4f,0.6f);
                var stone=CreateMeshObject("EntranceWeatheredRock",ctx.Props.Rocks[i%4],new[]{rock},parent).transform;
                stone.localPosition=p;stone.localScale=new Vector3(1.8f+(i%2)*0.4f,1.8f,2.4f);stone.localRotation=Quaternion.Euler(i*11,i*49+caveVariant*27,i*23);
            }
            for(int i=0;i<12;i++)
            {
                var stone=CreateMeshObject("CaveGravel",ctx.Props.Rocks[i%4],new[]{ctx.Mats.Rock},parent).transform;
                stone.localPosition=new Vector3((i%2==0?-1:1)*(3.4f-(i/4)*0.45f),0.05f,1+i*1.45f);
                stone.localScale=new Vector3(0.35f+(i%3)*0.12f,0.22f,0.45f);stone.localRotation=Quaternion.Euler(0,i*73,0);
            }
            for(int i=0;i<7;i++)
            {
                var bush=CreateMeshObject("EntranceFern",ctx.Props.Bushes[i%2],new[]{ctx.Mats.Bush},parent).transform;
                var p=CaveWorld(new Vector3((i%2==0?-1:1)*(7.4f+i*0.3f),0,-1-i*0.7f));
                p.y=RenderedCaveGround(ctx,p);bush.position=p;bush.localScale=Vector3.one*(0.5f+(i%3)*0.12f);
            }
            ctx.Count("hang động",1);
        }

        static void SaveCaveMesh(BuildContext ctx,Transform parent,FlatMeshBuilder builder,string name,Material[] mats)
        {
            var mesh=builder.ToMesh(name);ctx.Meshes.Add(mesh);
            var go=CreateMeshObject(name,mesh,mats,parent);
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        static float RenderedCaveGround(BuildContext ctx,Vector3 p)
        {
            var sample=ctx.Track.GetNearest(p);
            return sample.horizontalDistance>=ctx.Track.terrainMargin-MountainOverlap?MountainHeight(ctx.Track,p,sample):ctx.Track.TerrainHeightAt(p,sample.horizontalDistance,sample.WaterHeight);
        }

        static void CutCaveOpening(BuildContext ctx)
        {
            foreach(var group in new[]{"Terrain","Mountains"})
            {
                var root=ctx.Track.transform.Find("Generated/"+group);if(root==null)continue;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds=filter.GetComponent<Renderer>().bounds;
                    if(Vector3.Distance(bounds.ClosestPoint(caveOrigin),caveOrigin)>32)continue;
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                    var rebuilt=new FlatMeshBuilder(mesh.subMeshCount);
                    void Add(int sub,Vector3 a,Vector3 b,Vector3 c,int depth)
                    {
                        var mid=(a+b+c)/3;
                        var local=CaveLocal(filter.transform.TransformPoint(mid));
                        float radius=Mathf.Max((a-mid).magnitude,Mathf.Max((b-mid).magnitude,(c-mid).magnitude));
                        bool near=local.z> -3-radius&&local.z<26+radius&&local.y> -2-radius&&local.y<9+radius&&Mathf.Abs(local.x)<11+radius;
                        if(near&&depth<4&&radius>0.65f)
                        {
                            var ab=(a+b)*0.5f;var bc=(b+c)*0.5f;var ca=(c+a)*0.5f;
                            Add(sub,a,ab,ca,depth+1);Add(sub,ab,b,bc,depth+1);
                            Add(sub,ca,bc,c,depth+1);Add(sub,ab,bc,ca,depth+1);return;
                        }
                        float bend=Mathf.SmoothStep(0,1,local.z/24)*caveBend;
                        float width=Mathf.Lerp(4.85f,3.4f,Mathf.Clamp01(local.z/24));
                        float height=Mathf.Lerp(7.2f,5.9f,Mathf.Clamp01(local.z/24));
                        float x=(local.x-bend)/width,y=(local.y+0.1f)/height;
                        bool inside=local.z> -4.5f&&local.z<24.8f&&local.y> -0.3f&&x*x+y*y<1.08f;
                        if(!inside)rebuilt.Tri(sub,a,b,c);
                    }
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var indices=mesh.GetTriangles(sub);
                        for(int i=0;i<indices.Length;i+=3)Add(sub,vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]],0);
                    }
                    var refined=rebuilt.ToMesh(mesh.name);ctx.Meshes.Remove(mesh);ctx.Meshes.Add(refined);
                    filter.sharedMesh=refined;
                    var collider=filter.GetComponent<MeshCollider>();
                    if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=null;collider.sharedMesh=refined;

                }
            }
        }
    }
}
