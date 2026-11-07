using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class MountainCaveAccess
    {
        const string Folder="Assets/_Game/Generated/MountainCaveAccess";
        class Route {public string name;public Transform cave;public List<Vector3> points=new();public Vector3 entrance,outward;public Bounds bounds;public float half=3.25f,height=6.8f;}
        static readonly List<Route> routes=new();
        static Mesh Store(Mesh mesh,string name)
        {string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,saved);saved.SetVertices(mesh.vertices);saved.SetNormals(mesh.normals);saved.SetUVs(0,mesh.uv);for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);saved.RecalculateBounds();Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
        static void MeshObject(Transform root,Mesh mesh,Material mat,bool collider)
        {var g=new GameObject(mesh.name);g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);g.AddComponent<MeshFilter>().sharedMesh=Store(mesh,mesh.name);g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;}
        static bool Inside(Vector3 p,Route route)
        {
            if(!route.bounds.Contains(p))return false;
            float nearest=float.MaxValue;Vector3 center=Vector3.zero,right=Vector3.right;
            for(int i=0;i<route.points.Count-1;i++){var a=route.points[i];var b=route.points[i+1];var d=b-a;d.y=0;float u=Mathf.Clamp01(Vector3.Dot(new Vector3(p.x-a.x,0,p.z-a.z),d)/d.sqrMagnitude);var c=Vector3.Lerp(a,b,u);float distance=new Vector2(p.x-c.x,p.z-c.z).sqrMagnitude;if(distance<nearest){nearest=distance;center=c;right=Vector3.Cross(Vector3.up,d.normalized);}}
            var delta=p-center;float x=Vector3.Dot(delta,right)/(route.half+.28f),y=(delta.y+1.5f)/(route.height+1.5f+.25f);
            var first=route.points[0];var last=route.points[route.points.Count-1];
            return delta.y>-.1f&&x*x+y*y<1.04f&&Vector3.Dot(p-first,route.points[1]-first)>-3&&Vector3.Dot(p-last,last-route.points[route.points.Count-2])<3;
        }
        static int Carve(RiverTrack track)
        {
            var filters=new List<MeshFilter>();foreach(string group in new[]{"Terrain","Mountains","OriginalMountainRoofs"})filters.AddRange(track.transform.Find("Generated/"+group).GetComponentsInChildren<MeshFilter>());
            var repairs=track.transform.Find("Generated/MountainAccessGroundRepairs");if(repairs!=null)filters.AddRange(repairs.GetComponentsInChildren<MeshFilter>());
            filters.AddRange(track.transform.Find("Generated/BranchCaves").GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="GrandLimestoneVault"||f.name.StartsWith("SealedSideGalleryLining")||f.name=="ContinuousHighVaultLining"));
            int changed=0;
            foreach(var filter in filters)
            {
                var nearby=routes.Where(r=>r.bounds.Intersects(filter.GetComponent<Renderer>().bounds)).ToArray();if(nearby.Length==0)continue;var mesh=filter.sharedMesh;var vertices=mesh.vertices;var builder=new FlatMeshBuilder(mesh.subMeshCount);bool cut=false;
                void Add(int sub,Vector3 a,Vector3 b,Vector3 c,int depth)
                {
                    var mid=(a+b+c)/3;float size=Mathf.Max((a-b).magnitude,Mathf.Max((a-c).magnitude,(b-c).magnitude));bool near=nearby.Any(r=>r.bounds.SqrDistance(mid)<size*size);
                    if(near&&depth<4&&size>1.0f){var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;Add(sub,a,ab,ca,depth+1);Add(sub,ab,b,bc,depth+1);Add(sub,ca,bc,c,depth+1);Add(sub,ab,bc,ca,depth+1);return;}
                    if(nearby.Any(r=>Inside(mid,r))){cut=true;return;}builder.Tri(sub,filter.transform.InverseTransformPoint(a),filter.transform.InverseTransformPoint(b),filter.transform.InverseTransformPoint(c));
                }
                for(int sub=0;sub<mesh.subMeshCount;sub++){var tri=mesh.GetTriangles(sub);for(int i=0;i<tri.Length;i+=3)Add(sub,filter.transform.TransformPoint(vertices[tri[i]]),filter.transform.TransformPoint(vertices[tri[i+1]]),filter.transform.TransformPoint(vertices[tri[i+2]]),0);}
                if(!cut)continue;var replacement=builder.ToMesh(mesh.name);EditorUtility.CopySerialized(replacement,mesh);mesh.SetVertices(replacement.vertices);for(int i=0;i<replacement.subMeshCount;i++)mesh.SetTriangles(replacement.GetTriangles(i),i);mesh.UploadMeshData(false);Object.DestroyImmediate(replacement);var col=filter.GetComponent<MeshCollider>();if(col!=null){col.sharedMesh=null;col.sharedMesh=mesh;}if(filter.name=="GrandLimestoneVault"||filter.name=="ContinuousHighVaultLining"||filter.name.StartsWith("SealedSideGalleryLining"))CaveInteriorThemes.SmoothVault(mesh);EditorUtility.SetDirty(mesh);changed++;
            }
            return changed;
        }
        static void RestoreOldEntrances(RiverTrack track,Transform old)
        {
            if(old==null)return;var openings=old.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.EndsWith("_MountainEntrance")).Select(f=>f.GetComponent<Renderer>().bounds.center).ToArray();if(openings.Length==0)return;
            var generated=track.transform.Find("Generated");var repair=generated.Find("MountainAccessGroundRepairs");if(repair==null){repair=new GameObject("MountainAccessGroundRepairs").transform;repair.SetParent(generated,false);}
            var backup=AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Generated/BeforeShorelineFinish.asset").OfType<Mesh>().GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            var sources=new List<(Mesh mesh,Transform transform,Material[] materials,MeshCollider collider,int firstSub)>();
            var probes=new List<MeshCollider>();
            foreach(string group in new[]{"Terrain","Mountains"})foreach(var f in generated.Find(group).GetComponentsInChildren<MeshFilter>())if(backup.TryGetValue(f.sharedMesh.name,out var original))sources.Add((original,f.transform,f.GetComponent<Renderer>().sharedMaterials,f.GetComponent<MeshCollider>(),0));
            foreach(string group in new[]{"BranchCaves","Tunnels"})foreach(var f in generated.Find(group).GetComponentsInChildren<MeshFilter>(true))if(f.name=="CaveShell")
            {var roof=generated.Find("OriginalMountainRoofs/"+f.sharedMesh.name+"_OriginalMountainRoof");MeshCollider probe=null;if(roof!=null){probe=roof.GetComponent<MeshCollider>();if(probe==null){probe=roof.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=roof.GetComponent<MeshFilter>().sharedMesh;probes.Add(probe);}}sources.Add((f.sharedMesh,f.transform,f.GetComponent<Renderer>().sharedMaterials,probe,1));}
            Physics.SyncTransforms();
            foreach(var source in sources)
            {
                var builder=new FlatMeshBuilder(source.mesh.subMeshCount);var points=source.mesh.vertices;
                for(int sub=source.firstSub;sub<source.mesh.subMeshCount;sub++){var tri=source.mesh.GetTriangles(sub);for(int i=0;i<tri.Length;i+=3){var a=source.transform.TransformPoint(points[tri[i]]);var b=source.transform.TransformPoint(points[tri[i+1]]);var c=source.transform.TransformPoint(points[tri[i+2]]);var mid=(a+b+c)/3;if(Vector3.Cross(b-a,c-a).normalized.y<.03f||!openings.Any(p=>new Vector2(p.x-mid.x,p.z-mid.z).sqrMagnitude<169))continue;if(source.collider!=null&&source.collider.Raycast(new Ray(mid+Vector3.up*.08f,Vector3.down),out var hit,.16f))continue;builder.Tri(sub,a,b,c);}}
                if(builder.IsEmpty)continue;string name=source.mesh.name+"_AccessSurfaceRepair";var existing=repair.Find(name);if(existing!=null)Object.DestroyImmediate(existing.gameObject);
                var g=new GameObject(name);g.transform.SetParent(repair,false);g.AddComponent<MeshFilter>().sharedMesh=Store(builder.ToMesh(name),name);g.AddComponent<MeshRenderer>().sharedMaterials=source.materials;g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;
            }
            foreach(var probe in probes)Object.DestroyImmediate(probe);
        }
        public static string Build(RiverTrack track)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","MountainCaveAccess");
            var generated=track.transform.Find("Generated");var old=generated.Find("MountainCaveAccess");RestoreOldEntrances(track,old);if(old!=null)Object.DestroyImmediate(old.gameObject);var root=new GameObject("MountainCaveAccess").transform;root.SetParent(generated,false);routes.Clear();
            var temporary=new List<MeshCollider>();var ground=new HashSet<Collider>();
            foreach(string name in new[]{"Terrain","Mountains","OriginalMountainRoofs"})foreach(var f in generated.Find(name).GetComponentsInChildren<MeshFilter>()){var c=f.GetComponent<MeshCollider>();if(c==null){c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;temporary.Add(c);}ground.Add(c);}
            var repaired=generated.Find("MountainAccessGroundRepairs");if(repaired!=null)foreach(var col in repaired.GetComponentsInChildren<MeshCollider>())ground.Add(col);
            Physics.SyncTransforms();
            bool Ground(Vector3 p,out Vector3 hitPoint)
            {hitPoint=p;bool found=false;foreach(var hit in Physics.RaycastAll(new Vector3(p.x,230,p.z),Vector3.down,300))if(ground.Contains(hit.collider)&&(!found||hit.point.y>hitPoint.y)){hitPoint=hit.point;found=true;}return found;}
            var treePoints=new List<Vector3>();foreach(string group in new[]{"Trees","DeepForest","MountainForest"})foreach(Transform tree in generated.Find(group))treePoints.Add(tree.position);
            foreach(Transform cave in generated.Find("BranchCaves"))
            {
                var frames=CaveInteriorThemes.Frames(track,cave);if(frames.Count<4)continue;
                foreach(int side in new[]{-1,1})
                {
                    var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];
                    var outward=(f.right*side+f.forward*.15f).normalized;var end=f.p+f.right*side*(f.half*.72f+24)+f.forward*7+Vector3.up*.97f;var start=end-outward*2;
                    Vector3 target=end+outward*55;float best=float.MinValue;
                    for(float length=18;length<=82;length+=8)foreach(float angle in new[]{0f,-30f,30f,-60f,60f,-90f,90f})
                    {
                        var direction=Quaternion.AngleAxis(angle,Vector3.up)*outward;var p=end+direction*length;if(!Ground(p,out var surface)||surface.y<start.y+6||surface.y>100)continue;
                        if(treePoints.Any(tree=>new Vector2(tree.x-p.x,tree.z-p.z).sqrMagnitude<16))continue;
                        if(routes.Any(r=>new Vector2(r.entrance.x-p.x,r.entrance.z-p.z).sqrMagnitude<400))continue;
                        p.y=surface.y-2.4f;float grade=(p.y-start.y)/length;if(grade>.70f)continue;int covered=0;
                        for(int i=1;i<=8;i++){float u=i/10f;var q=Vector3.Lerp(start,p,u);if(Ground(q,out var g)&&g.y>q.y+7.5f)covered++;}
                        float score=covered*14-length*.12f-Mathf.Abs(grade-.35f)*12+surface.y*.04f;if(score>best){best=score;target=p;}
                    }
                    if(best==float.MinValue){if(Ground(target,out var surface))target.y=surface.y-2.4f;else target.y=start.y+20;}
                    var route=new Route{name=cave.name+(side<0?"_LeftMountainPassage":"_RightMountainPassage"),cave=cave,entrance=target,outward=(target-start).normalized};route.outward.y=0;route.outward.Normalize();
                    int sections=Mathf.CeilToInt(Vector3.Distance(start,target)/1.8f);
                    for(int i=0;i<=sections;i++){float u=i/(float)sections;var p=Vector3.Lerp(start,target,u);p+=f.forward*Mathf.Sin(u*Mathf.PI)*(side<0?2.5f:-2.5f);route.points.Add(p);}
                    var outside=target+route.outward*9;if(Ground(outside,out var gOutside))outside.y=gOutside.y+.08f;else outside.y=target.y+2.4f;route.points.Add(outside);
                    route.bounds=new Bounds(start,Vector3.zero);foreach(var p in route.points){route.bounds.Encapsulate(p+Vector3.up*8);route.bounds.Encapsulate(p);}route.bounds.Expand(new Vector3(10,2,10));routes.Add(route);
                }
            }
            int carved=Carve(track);foreach(var c in temporary)Object.DestroyImmediate(c);
            var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Rock.mat");var sand=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Cliff.mat");
            var visibility=track.GetComponent<CaveInteriorVisibility>();var centers=new List<Vector4>(visibility.centers);var axes=new List<Vector4>(visibility.axes);
            foreach(var route in routes)
            {
                var group=new GameObject(route.name).transform;group.SetParent(root,false);var wall=new FlatMeshBuilder();var stairs=new FlatMeshBuilder();const int arc=32;
                Vector3 Right(int i){var a=route.points[Mathf.Max(0,i-1)];var b=route.points[Mathf.Min(route.points.Count-1,i+1)];var d=b-a;d.y=0;return Vector3.Cross(Vector3.up,d.normalized);}
                Vector3 Edge(int row,int col){var p=route.points[row];float angle=col*Mathf.PI/arc;float n=.10f*Mathf.Sin(row*1.7f+col*.83f);return p+Right(row)*Mathf.Cos(angle)*(route.half+n)+Vector3.up*(-.6f+Mathf.Sin(angle)*(route.height+.6f+n));}
                for(int row=0;row<route.points.Count-2;row++)for(int col=0;col<arc;col++){var a=Edge(row,col);var b=Edge(row+1,col);var c=Edge(row,col+1);var d=Edge(row+1,col+1);var center=(route.points[row]+route.points[row+1])*.5f+Vector3.up*2;var normal=Vector3.Cross(b-a,c-a);if(Vector3.Dot(normal,center-a)<0){wall.Tri(0,a,c,b);wall.Tri(0,b,c,d);}else{wall.Tri(0,a,b,c);wall.Tri(0,b,d,c);}}
                for(int row=0;row<route.points.Count-1;row++)
                {
                    var a=route.points[row];var b=route.points[row+1];int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(b.y-a.y)/.20f));
                    for(int step=0;step<steps;step++){float u0=step/(float)steps,u1=(step+1)/(float)steps;var p=Vector3.Lerp(a,b,u0);var q=Vector3.Lerp(a,b,u1);var right=Vector3.Lerp(Right(row),Right(row+1),(u0+u1)*.5f).normalized;float height=Mathf.Max(p.y,q.y);var l=p-right*3.2f;var r=p+right*3.2f;var nl=q-right*3.2f;var nr=q+right*3.2f;l.y=r.y=nl.y=nr.y=height;stairs.TriUp(0,l,nl,r);stairs.TriUp(0,r,nl,nr);var bottomL=nl;var bottomR=nr;bottomL.y=bottomR.y=Mathf.Min(p.y,q.y)-.03f;stairs.Tri(0,nl,bottomL,nr);stairs.Tri(0,nr,bottomL,bottomR);}
                    if(row%4==0){var p=(a+b)*.5f;centers.Add(new Vector4(p.x,p.y,p.z,3.8f));var right=Right(row);axes.Add(new Vector4(right.x,right.z,7.3f,4.5f+(route.cave.GetSiblingIndex()+1)*100));}
                    if(row%8==4){var l=new GameObject("RecessedPassageLight").AddComponent<Light>();l.transform.SetParent(group,false);l.transform.position=a+Right(row)*2.4f+Vector3.up*2;l.type=LightType.Point;l.color=new Color(1,.80f,.55f);l.intensity=2.8f;l.range=11;l.shadows=LightShadows.None;}
                }
                MeshObject(group,wall.ToMesh(route.name+"_Interior"),rock,true);MeshObject(group,stairs.ToMesh(route.name+"_StoneStairs"),sand,true);
                var archMesh=new FlatMeshBuilder();var rightDoor=Vector3.Cross(Vector3.up,route.outward);var origin=route.entrance;
                Vector3 Arch(int index,bool outer,float depth){float a=index*Mathf.PI/32;float noise=.12f*Mathf.Sin(index*2.3f);return origin+rightDoor*Mathf.Cos(a)*((outer?4.8f:3.3f)+noise)+Vector3.up*(Mathf.Sin(a)*((outer?8.1f:6.7f)+noise)-.4f)+route.outward*depth;}
                for(int i=0;i<32;i++){var a=Arch(i,false,.3f);var b=Arch(i+1,false,.3f);var c=Arch(i,true,-.3f);var d=Arch(i+1,true,-.3f);archMesh.TriOutward(0,a,c,b,origin-route.outward*4+Vector3.up*2);archMesh.TriOutward(0,b,c,d,origin-route.outward*4+Vector3.up*2);var e=Arch(i,false,-2);var f=Arch(i+1,false,-2);archMesh.TriOutward(0,a,b,e,origin+rightDoor*20);archMesh.TriOutward(0,b,f,e,origin+rightDoor*20);}
                MeshObject(group,archMesh.ToMesh(route.name+"_MountainEntrance"),rock,true);
                foreach(var renderer in route.cave.Find("MajesticChambers").GetComponentsInChildren<MeshRenderer>())
                {if(!(renderer.name.Contains("Column")||renderer.name.Contains("Dome")||renderer.name.Contains("Stalagmite")||renderer.name=="RoyalOrganFlowstone"))continue;var p=renderer.transform.position;if(new Vector2(p.x-route.points[0].x,p.z-route.points[0].z).magnitude<4.5f&&Mathf.Abs(p.y-route.points[0].y)<2)renderer.gameObject.SetActive(false);}
            }
            visibility.centers=centers.Take(256).ToArray();visibility.axes=axes.Take(256).ToArray();visibility.Upload();EditorUtility.SetDirty(visibility);Physics.SyncTransforms();AssetDatabase.SaveAssets();return $"Built {routes.Count} connected uphill passages with six mountain entrances; carved {carved} mesh sections.";
        }
    }
}


