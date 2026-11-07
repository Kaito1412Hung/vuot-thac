using System.Linq;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class CaveMountainExterior
    {
        const string Folder="Assets/_Game/Generated/CaveMountainExterior";
        static Mesh Store(Mesh mesh,string name)
        {
            string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);saved.SetVertices(mesh.vertices);saved.SetNormals(mesh.normals);saved.SetUVs(0,mesh.uv);for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);
            saved.RecalculateBounds();Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }
        static void Add(Transform root,Mesh mesh,Material[] materials,Transform source)
        {
            var obj=new GameObject(mesh.name);obj.transform.SetParent(root,false);obj.transform.SetPositionAndRotation(source.position,source.rotation);obj.transform.localScale=source.lossyScale;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
        public static string Restore(RiverTrack track,bool restoreMissingMountainFaces=false)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CaveMountainExterior");
            var generated=track.transform.Find("Generated");var root=generated.Find("OriginalMountainRoofs");
            if(root!=null&&restoreMissingMountainFaces){Object.DestroyImmediate(root.gameObject);root=null;}
            if(root==null){root=new GameObject("OriginalMountainRoofs").transform;root.SetParent(generated,false);}
            else foreach(var child in root.Cast<Transform>().Where(t=>t.name.EndsWith("_OriginalMountainRoof")).ToArray())Object.DestroyImmediate(child.gameObject);
            int roofs=0,faces=0;
            // Keep all new interior triangles. Only remove the exposed brown outer envelope.
            foreach(var f in generated.GetComponentsInChildren<MeshFilter>(true))
            {
                if(f.name=="GrandLimestoneVault"&&f.sharedMesh.subMeshCount>1)
                {f.sharedMesh.SetTriangles(new int[0],1);f.sharedMesh.RecalculateBounds();EditorUtility.SetDirty(f.sharedMesh);var c=f.GetComponent<MeshCollider>();if(c!=null){c.sharedMesh=null;c.sharedMesh=f.sharedMesh;}}
                if(f.name!="CaveShell")continue;
                var mesh=Object.Instantiate(f.sharedMesh);mesh.name=f.sharedMesh.name+"_OriginalMountainRoof";mesh.SetTriangles(new int[0],0);
                Add(root,Store(mesh,mesh.name),f.GetComponent<Renderer>().sharedMaterials,f.transform);roofs++;
            }
            if(restoreMissingMountainFaces)
            {
                var caveBounds=generated.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="GrandLimestoneVault").Select(f=>f.GetComponent<Renderer>().bounds).ToArray();
                var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Generated/BeforeShorelineFinish.asset").OfType<Mesh>().GroupBy(x=>x.name).ToDictionary(x=>x.Key,x=>x.First());
                foreach(string group in new[]{"Terrain","Mountains"})foreach(var f in generated.Find(group).GetComponentsInChildren<MeshFilter>())
                {
                    if(!source.TryGetValue(f.sharedMesh.name,out var original))continue;
                    var builder=new FlatMeshBuilder(original.subMeshCount);var points=original.vertices;var collider=f.GetComponent<MeshCollider>();
                    for(int sub=0;sub<original.subMeshCount;sub++)
                    {
                        var tri=original.GetTriangles(sub);for(int i=0;i<tri.Length;i+=3)
                        {
                            var a=points[tri[i]];var b=points[tri[i+1]];var c=points[tri[i+2]];var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<.03f)continue;
                            var p=f.transform.TransformPoint((a+b+c)/3);
                            if(!caveBounds.Any(bounds=>bounds.SqrDistance(p)<9&&p.y>bounds.min.y+8))continue;
                            if(collider!=null&&collider.Raycast(new Ray(p+Vector3.up*.08f,Vector3.down),out var hit,.16f))continue;
                            builder.Tri(sub,a,b,c);faces++;
                        }
                    }
                    if(builder.IsEmpty)continue;string name=f.sharedMesh.name+"_RestoredMountainSurface";
                    Add(root,Store(builder.ToMesh(name),name),f.GetComponent<Renderer>().sharedMaterials,f.transform);
                }
            }
            return $"Restored {roofs} original mountain roofs and {faces} mountain surface triangles; interior meshes preserved.";
        }
    }
}
