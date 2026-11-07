using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class CaveAccessFinisher
    {
        const string Folder="Assets/_Game/Generated/MountainCaveAccess";
        static Mesh Store(Mesh mesh)
        {string path=Folder+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,saved);saved.SetVertices(mesh.vertices);saved.SetNormals(mesh.normals);saved.SetUVs(0,mesh.uv);for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);saved.RecalculateBounds();Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
        static void ObjectMesh(Transform parent,Mesh mesh,Material[] materials,bool collider)
        {var obj=new GameObject(mesh.name);obj.transform.SetParent(parent,false);obj.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);obj.AddComponent<MeshFilter>().sharedMesh=Store(mesh);obj.AddComponent<MeshRenderer>().sharedMaterials=materials;if(collider)obj.AddComponent<MeshCollider>().sharedMesh=obj.GetComponent<MeshFilter>().sharedMesh;}
        static (Vector3 current,Vector3 next) RightBoundary(Vector3[] vertices,int offset)
        {
            int a=0,b=1;float best=float.MaxValue;for(int i=0;i<3;i++)for(int j=i+1;j<3;j++){float d=new Vector2(vertices[offset+i].x-vertices[offset+j].x,vertices[offset+i].z-vertices[offset+j].z).sqrMagnitude;if(d<best){best=d;a=i;b=j;}}
            int other=3-a-b;var low=vertices[offset+a].y<vertices[offset+b].y?vertices[offset+a]:vertices[offset+b];return(low,vertices[offset+other]);
        }
        public static string Finish(RiverTrack track)
        {
            var generated=track.transform.Find("Generated");var root=generated.Find("MountainCaveAccess");var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Rock.mat");var grass=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/GrassDark.mat");
            string path=Folder+"/AccessStoneDoubleSided.mat";var solid=AssetDatabase.LoadAssetAtPath<Material>(path);if(solid==null){solid=new Material(rock);AssetDatabase.CreateAsset(solid,path);}solid.SetFloat("_Cull",0);EditorUtility.SetDirty(solid);int count=0;
            foreach(Transform group in root)
            {
                var old=group.Find("NaturalEntranceFinish");if(old!=null)Object.DestroyImmediate(old.gameObject);var finish=new GameObject("NaturalEntranceFinish").transform;finish.SetParent(group,false);
                var wall=group.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith("_Interior"));var vertices=wall.sharedMesh.vertices;const int stride=32*6;int rows=vertices.Length/stride;var points=new List<Vector3>();
                for(int row=0;row<rows;row++){var right=RightBoundary(vertices,row*stride).current;var left=RightBoundary(vertices,row*stride+31*6).current;points.Add(wall.transform.TransformPoint((right+left)*.5f+Vector3.up*.6f));}
                var lastRight=RightBoundary(vertices,(rows-1)*stride).next;int o=(rows-1)*stride+31*6+3;var lastLeft=RightBoundary(vertices,o).current;points.Add(wall.transform.TransformPoint((lastRight+lastLeft)*.5f+Vector3.up*.6f));
                var outward=points[points.Count-1]-points[0];outward.y=0;outward.Normalize();var endpoint=points[points.Count-1];var stair=group.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith("_StoneStairs"));var sv=stair.sharedMesh.vertices.Select(p=>stair.transform.TransformPoint(p)).ToArray();float furthest=sv.Max(p=>Vector3.Dot(p-endpoint,outward));var edge=sv.Where(p=>Vector3.Dot(p-endpoint,outward)>furthest-.025f).ToArray();float height=edge.Max(p=>p.y);var target=edge.Aggregate(Vector3.zero,(a,b)=>a+b)/edge.Length;target.y=height;points.Add(target);
                var route=group.GetComponent<CavePassagePath>();if(route==null)route=group.gameObject.AddComponent<CavePassagePath>();route.points=points.ToArray();EditorUtility.SetDirty(route);
                Vector3 Right(int i){var d=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];d.y=0;return Vector3.Cross(Vector3.up,d.normalized);}
                var floor=new FlatMeshBuilder();for(int i=0;i<points.Count-1;i++){var a=points[i]-Right(i)*4.1f-Vector3.up*.12f;var b=points[i]+Right(i)*4.1f-Vector3.up*.12f;var c=points[i+1]-Right(i+1)*4.1f-Vector3.up*.12f;var d=points[i+1]+Right(i+1)*4.1f-Vector3.up*.12f;floor.TriUp(0,a,c,b);floor.TriUp(0,b,c,d);}ObjectMesh(finish,floor.ToMesh(group.name+"_SolidRockFoundation"),new[]{solid},true);
                var banks=new FlatMeshBuilder(2);var rampStart=points[points.Count-2];var rampEnd=points[points.Count-1];var rampRight=Vector3.Cross(Vector3.up,(rampEnd-rampStart).normalized).normalized;
                for(int i=0;i<16;i++)foreach(int side in new[]{-1,1})
                {
                    float u=i/16f,v=(i+1)/16f;var p=Vector3.Lerp(rampStart,rampEnd,u);var q=Vector3.Lerp(rampStart,rampEnd,v);
                    var a=p+rampRight*side*3.8f-Vector3.up*4;var b=q+rampRight*side*3.8f-Vector3.up*4;
                    var c=p+rampRight*side*4.2f+Vector3.up*(4.8f*(1-u));var d=q+rampRight*side*4.2f+Vector3.up*(4.8f*(1-v));
                    banks.Tri(1,a,b,c);banks.Tri(1,b,d,c);var e=p+rampRight*side*7.0f+Vector3.up*.05f;var f=q+rampRight*side*7.0f+Vector3.up*.05f;banks.TriUp(0,c,d,e);banks.TriUp(0,d,f,e);
                }
                ObjectMesh(finish,banks.ToMesh(group.name+"_ClosedEntranceBanks"),new[]{grass,solid},true);
                var mound=new FlatMeshBuilder(2);int start=Mathf.Max(0,points.Count-10),end=points.Count-2;const int arc=32;
                Vector3 Roof(int r,int c){float a=c*Mathf.PI/arc;return points[r]+Right(r)*Mathf.Cos(a)*4.8f+Vector3.up*(-.8f+Mathf.Sin(a)*9.0f+.08f*Mathf.Sin(c*1.9f+r));}
                void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 inside){var n=Vector3.Cross(b-a,c-a);if(Vector3.Dot(n,(a+b+c)/3-inside)<0){var t=b;b=c;c=t;n=-n;}mound.Tri(n.normalized.y>.6f?0:1,a,b,c);}
                for(int r=start;r<end;r++)for(int c=0;c<arc;c++){var a=Roof(r,c);var b=Roof(r+1,c);var d=Roof(r,c+1);var e=Roof(r+1,c+1);var inside=(points[r]+points[r+1])*.5f+Vector3.up*2;Face(a,b,d,inside);Face(b,e,d,inside);}ObjectMesh(finish,mound.ToMesh(group.name+"_GrassyMountainCanopy"),new[]{grass,solid},true);
                wall.GetComponent<Renderer>().sharedMaterial=solid;foreach(var f in group.GetComponentsInChildren<MeshFilter>())if(f.name.EndsWith("_MountainEntrance"))f.GetComponent<Renderer>().sharedMaterial=solid;count++;
            }
            RebuildVisibility(track);Physics.SyncTransforms();AssetDatabase.SaveAssets();return "Finished "+count+" mountain entrances, added solid foundations and grass-covered rock canopies.";
        }
        public static int RebuildVisibility(RiverTrack track)
        {
            var centers=new List<Vector4>();var axes=new List<Vector4>();int owner=0;
            foreach(Transform cave in track.transform.Find("Generated/BranchCaves").Cast<Transform>().Concat(track.transform.Find("Generated/Tunnels").Cast<Transform>()))
            {
                owner++;var frames=CaveInteriorThemes.Frames(track,cave);
                for(int i=0;i<frames.Count;i+=3){var f=frames[i];centers.Add(new Vector4(f.p.x,f.p.y,f.p.z,f.half));axes.Add(new Vector4(f.right.x,f.right.z,f.height,4.1f+owner*100));}
                foreach(int side in new[]{-1,1})
                {var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];
                    for(int i=0;i<=4;i++){float u=i/4f;var dir=(f.right*side+f.forward*(.22f*Mathf.Sin(u*2))).normalized;var right=Vector3.Cross(Vector3.up,dir).normalized;var p=f.p+f.right*side*(f.half*.72f+u*24)+f.forward*(u*u*7)+Vector3.up*.85f;centers.Add(new Vector4(p.x,p.y,p.z,Mathf.Lerp(5.6f,7.4f,Mathf.Sin(u*Mathf.PI*.75f))));axes.Add(new Vector4(right.x,right.z,Mathf.Lerp(9.5f,13.5f,Mathf.Sin(u*Mathf.PI*.8f)),4.7f+owner*100));}
                }
            }
            foreach(var route in track.transform.Find("Generated/MountainCaveAccess").GetComponentsInChildren<CavePassagePath>())
            {
                owner=route.name.StartsWith("BranchCave_0_0")?1:route.name.StartsWith("BranchCave_0_1")?2:3;var points=route.points;
                for(int i=0;i<points.Length-2;i+=4){var p=(points[i]+points[i+1])*.5f;var delta=points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)];delta.y=0;var right=Vector3.Cross(Vector3.up,delta.normalized);centers.Add(new Vector4(p.x,p.y,p.z,3.8f));axes.Add(new Vector4(right.x,right.z,7.3f,4.5f+owner*100));}
            }
            var visibility=track.GetComponent<CaveInteriorVisibility>();visibility.centers=centers.Take(256).ToArray();visibility.axes=axes.Take(256).ToArray();visibility.Upload();EditorUtility.SetDirty(visibility);
            string[] names={"AmberCurtainSanctuary","JadeMirrorGrotto","IvoryCathedral","PearlFlowerChamber","CopperCanyonHall"};
            for(int i=0;i<names.Length;i++){var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/CaveInteriorThemes/"+names[i]+"_HighVaultLining.mat");if(mat!=null){mat.SetFloat("_CaveOwner",i+1);EditorUtility.SetDirty(mat);}}
            return centers.Count;
        }
        public static string Audit(RiverTrack track)
        {
            int tested=0,blocked=0;string notes="";
            foreach(var route in track.transform.Find("Generated/MountainCaveAccess").GetComponentsInChildren<CavePassagePath>())
            for(int i=1;i<route.points.Length;i++){var a=route.points[i-1]+Vector3.up*1.7f;var b=route.points[i]+Vector3.up*1.7f;tested++;foreach(var hit in Physics.SphereCastAll(a,.28f,(b-a).normalized,Vector3.Distance(a,b),~0,QueryTriggerInteraction.Ignore)){blocked++;if(blocked<10)notes+=route.name+" segment "+i+" "+hit.collider.name+"\n";break;}}
            return $"Tested {tested} walking segments: {blocked} obstructed.\n"+notes;
        }
    }
}

