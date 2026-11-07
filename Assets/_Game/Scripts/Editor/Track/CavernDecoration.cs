using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class CavernDecoration
    {
        const string Folder="Assets/_Game/Generated/CavernDetails";
        static Material Material(string name,Color color,float smooth=0.25f)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
        }
        static Mesh Formation(string name,bool hanging)
        {
            string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int sides=20,rows=16;
            for(int j=0;j<=rows;j++)for(int i=0;i<=sides;i++)
            {
                float t=j/(float)rows,a=i/(float)sides*Mathf.PI*2;
                float radius=Mathf.Pow(1-t,hanging?1.3f:.65f)*(1+.1f*Mathf.Sin(t*24)+.035f*Mathf.Sin(t*73));
                radius*=1+.09f*Mathf.Sin(a*7+t*3)+.04f*Mathf.Cos(a*13-t*5);
                v.Add(new Vector3(Mathf.Cos(a)*radius+.06f*t*t, (hanging?-1:1)*t,Mathf.Sin(a)*radius+.035f*Mathf.Sin(t*4)));
                uv.Add(new Vector2(i/(float)sides,t*3));
                if(j<rows&&i<sides){int k=j*(sides+1)+i;foreach(int n in hanging?new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1}:new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2})tri.Add(n);}
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Transform Object(Transform parent,string name,Mesh mesh,Material material,Vector3 p,Vector3 scale)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=p;g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g.transform;}
        public static string Build(RiverTrack track)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CavernDetails");
            var cream=Material("IvoryCalcite",new Color(.66f,.57f,.43f));var ochre=Material("GoldenFlowstone",new Color(.48f,.34f,.20f));
            var pale=Material("PearlLimestone",new Color(.75f,.72f,.61f),.42f);var wet=Material("WetMineralStone",new Color(.24f,.31f,.28f),.75f);
            var water=Material("StillTurquoisePool",new Color(.055f,.29f,.31f),.94f);
            var down=Formation("LayeredStalactite",true);var up=Formation("RoundedStalagmite",false);
            int caves=0,formations=0;var rng=new System.Random(76291);
            float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
            foreach(string category in new[]{"ForestCave","Tunnels","BranchCaves"})
            foreach(Transform cave in track.transform.Find("Generated/"+category))
            {
                if(!cave.gameObject.activeInHierarchy)continue;
                var old=cave.Find("NaturalCavernDetails");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("NaturalCavernDetails").transform;root.SetParent(cave,false);
                bool forest=category=="ForestCave";
                var shell=cave.Find(forest?"CurvedCaveInterior":"CaveShell").GetComponent<MeshCollider>();
                var legacy=cave.Find("CaveDetail");if(legacy!=null)legacy.gameObject.SetActive(false);
                var frames=new List<(Vector3 center,Vector3 across,float half)>();
                if(forest)
                {
                    int variant=int.Parse(cave.name.Split('_')[1]);float bend=0;
                    foreach(var profile in track.GetComponents<ForestCaveLandform>())if(profile.variant==variant)bend=profile.bend;
                    for(float z=3;z<23;z+=3){var p=new Vector3(Mathf.SmoothStep(0,1,z/24)*bend,.08f,z);frames.Add((cave.TransformPoint(p),cave.right,Mathf.Lerp(4.7f,3.2f,z/24)));}
                }
                else if(category=="Tunnels")
                {
                    var tunnel=track.tunnels[int.Parse(cave.name.Split('_')[1])];
                    for(float d=tunnel.distance-tunnel.length*.5f+4;d<tunnel.distance+tunnel.length*.5f-3;d+=5)
                    {var s=track.EvaluateAtDistance(d);var l=track.AfterRaceInsertion(s.center-s.right*track.HalfWidth);var r=track.AfterRaceInsertion(s.center+s.right*track.HalfWidth);frames.Add(((l+r)*.5f,(r-l).normalized,Vector3.Distance(l,r)*.5f));}
                }
                else
                {
                    int bi=int.Parse(cave.name.Split('_')[1]);var b=track.Branches[bi];var bounds=shell.bounds;
                    for(float d=8;d<b.Length-8;d+=5){var s=b.EvaluateAtDistance(d);var p=s.center+Vector3.right*b.side*track.appliedRaceStripWidth*.5f;if(bounds.Contains(p+Vector3.up*2)&&shell.Raycast(new Ray(p+Vector3.up,Vector3.up),out var roof,25))frames.Add((p,s.right,b.HalfWidth));}
                }
                int fi=0;
                foreach(var frame in frames)
                {
                    var p=frame.center;var across=frame.across;
                    for(int k=0;k<(forest?7:9);k++)
                    {
                        float fraction=R(-.92f,.92f);var rayPoint=p+across*(frame.half*fraction)+Vector3.up*.8f;
                        if(!shell.Raycast(new Ray(rayPoint,Vector3.up),out var hit,40))continue;
                        float clearance=hit.point.y-p.y;float length=Mathf.Min(R(.65f,forest?3.4f:4.7f),clearance-(forest?2.8f:4.7f));if(length<.45f)continue;
                        var top=hit.point+Vector3.up*.15f;float radius=length*R(.16f,.28f);
                        var obj=Object(root,"CalcitePendant",down,k%3==0?ochre:cream,top,new Vector3(radius,length,radius*R(.7f,1.1f)));
                        obj.rotation=Quaternion.Euler(R(-4,4),R(0,360),R(-4,4));formations++;
                        // Small satellite drips make naturally grouped formations.
                        if(k%2==0){Object(root,"SodaStrawCluster",down,pale,top+across*.3f,new Vector3(.075f,length*.6f,.075f));formations++;}
                    }
                    foreach(float side in new[]{-1f,1f})
                    {
                        var foot=p+across*side*(frame.half*(forest?.76f:1.03f));foot.y=p.y-(forest?0:.25f);
                        float height=R(forest?.8f:1.3f,forest?2.6f:4.2f),radius=R(.45f,forest?.95f:1.5f);
                        Object(root,"TerracedStalagmite",up,fi%3==0?wet:pale,foot,new Vector3(radius,height,radius*.8f));formations++;
                        for(int j=0;j<3;j++){var q=foot+across*side*R(.2f,.8f)+Vector3.Cross(across,Vector3.up)*R(-.8f,.8f);Object(root,"MineralTerrace",up,ochre,q,new Vector3(radius*R(.7f,1.3f),height*R(.15f,.38f),radius));formations++;}
                    }
                    if(fi%3==1)
                    {
                        var light=new GameObject("ConcealedWarmBounce").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.position=p+across*(fi%2==0?-1:1)*frame.half*.68f+Vector3.up*(forest?1.6f:2.4f);
                        light.type=LightType.Point;light.color=fi%2==0?new Color(1,.77f,.49f):new Color(.50f,.76f,.78f);light.intensity=forest?1.8f:2.6f;light.range=forest?11:18;light.shadows=LightShadows.None;
                    }
                    fi++;
                }
                if(forest&&frames.Count>3)
                {
                    var f=frames[frames.Count-2];var p=f.center+f.across*f.half*.58f+Vector3.up*.045f;
                    var mesh=new Mesh{name="RimstonePool"};var v=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
                    for(int i=0;i<=40;i++){float a=i*Mathf.PI*2/40;v.Add(new Vector3(Mathf.Cos(a)*(1+.1f*Mathf.Sin(a*5)),0,Mathf.Sin(a)*1.7f));if(i<40){triangles.Add(0);triangles.Add(i+2);triangles.Add(i+1);}}
                    mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();string path=Folder+"/Pool_"+cave.name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(stored==null){AssetDatabase.CreateAsset(mesh,path);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}
                    Object(root,"QuietRimstonePool",stored,water,p,Vector3.one);
                }
                caves++;
            }
            AssetDatabase.SaveAssets();return $"Decorated {caves} caves with {formations} natural limestone formations.";
        }
    }
}
