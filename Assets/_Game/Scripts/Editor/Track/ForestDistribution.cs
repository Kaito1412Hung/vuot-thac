using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class ForestDistribution
    {
        class Tree { public Transform transform; public float radius;public Vector3 p;public bool invalidGround; }
        struct Site { public Vector3 p;public float upland; }
        public static string LastReport {get;private set;}
        public static string Balance(RiverTrack track)
        {
            var generated=track.transform.Find("Generated");var trees=new List<Tree>();
            foreach(string name in new[]{"Trees","DeepForest","MountainForest"})foreach(Transform tr in generated.Find(name))
            {
                var r=tr.GetComponentInChildren<Renderer>();float radius=r==null?2.5f:Mathf.Max(r.bounds.extents.x,r.bounds.extents.z);
                trees.Add(new Tree{transform=tr,p=tr.position,radius=Mathf.Clamp(radius,1.8f,4.5f)});
            }
            var temporary=new List<MeshCollider>();var ground=new HashSet<Collider>();
            foreach(string name in new[]{"Terrain","Mountains","OriginalMountainRoofs"})
            {
                var group=generated.Find(name);if(group==null)continue;
                foreach(var f in group.GetComponentsInChildren<MeshFilter>())
                {var collider=f.GetComponent<MeshCollider>();if(collider==null){collider=f.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;temporary.Add(collider);}ground.Add(collider);}
            }
            var water=new List<MeshCollider>();
            foreach(var f in generated.GetComponentsInChildren<MeshFilter>())
            if(f.name=="CreekToMainRiver"||f.name=="MountainHeadwaterBasin")
            {var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;temporary.Add(c);water.Add(c);}
            var protectedAreas=new List<Bounds>();
            foreach(var f in generated.GetComponentsInChildren<MeshFilter>())
            {
                string name=f.sharedMesh==null?"":f.sharedMesh.name;
                if(name=="Cabin"||name=="HouseTall"||f.name=="CaveMouthBoulder"||f.name=="EntranceWeatheredRock"||f.name.EndsWith("_MountainEntrance"))
                {var b=f.GetComponent<Renderer>().bounds;b.Expand(new Vector3(7,15,7));protectedAreas.Add(b);}
            }
            foreach(Transform c in generated.Find("ForestCave")){var b=new Bounds(c.position+Vector3.up*3,new Vector3(22,24,22));protectedAreas.Add(b);}
            var access=generated.Find("MountainCaveAccess");if(access!=null)foreach(var route in access.GetComponentsInChildren<CavePassagePath>())if(route.points.Length>1)
            {var a=route.points[route.points.Length-2];var b=route.points[route.points.Length-1];var bounds=new Bounds((a+b)*.5f+Vector3.up*3,new Vector3(Mathf.Abs(a.x-b.x)+12,16,Mathf.Abs(a.z-b.z)+12));protectedAreas.Add(bounds);}
            Physics.SyncTransforms();var rng=new System.Random(track.seed+68293);
            float Random(float min,float max)=>Mathf.Lerp(min,max,(float)rng.NextDouble());
            var cells=new Dictionary<Vector2Int,List<Tree>>();
            Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/12),Mathf.FloorToInt(p.z/12));
            void Register(Tree tree){var key=Cell(tree.p);if(!cells.TryGetValue(key,out var list)){list=new List<Tree>();cells.Add(key,list);}list.Add(tree);}
            float Nearest(Vector3 p,float radius,out int neighbors)
            {
                float best=42;neighbors=0;var key=Cell(p);
                for(int x=-4;x<=4;x++)for(int z=-4;z<=4;z++)if(cells.TryGetValue(key+new Vector2Int(x,z),out var list))foreach(var t in list)
                {float d=new Vector2(p.x-t.p.x,p.z-t.p.z).magnitude;if(d<18)neighbors++;if(d<Mathf.Max(6,(radius+t.radius)*.86f+1))return -1;best=Mathf.Min(best,d);}
                return best;
            }
            bool Ground(Vector3 p,out Vector3 position,out float upland)
            {
                position=p;upland=0;RaycastHit highest=new RaycastHit();bool found=false;
                foreach(var hit in Physics.RaycastAll(new Vector3(p.x,240,p.z),Vector3.down,320,~0,QueryTriggerInteraction.Ignore))
                if(ground.Contains(hit.collider)&&(!found||hit.point.y>highest.point.y)){highest=hit;found=true;}
                if(!found||highest.normal.y<.78f)return false;
                position=highest.point;int hint=0;var sample=track.SampleWater(position,ref hint);
                bool mountainCover=position.y>sample.height+16&&(highest.collider.name.Contains("MountainRoof")||highest.collider.name.Contains("RestoredMountainSurface")||highest.collider.name.StartsWith("Mountains"));
                if((sample.edgeDistance<5.8f&&!mountainCover)||position.y<sample.height+1.2f)return false;
                foreach(var b in protectedAreas)if(b.Contains(position+Vector3.up*2))return false;
                foreach(var c in water)if(c.Raycast(new Ray(position+Vector3.up*100,Vector3.down),out var hit,200)&&Mathf.Abs(hit.point.y-position.y)<2.5f)return false;
                upland=position.y-sample.height;if(upland>72)return false;return true;
            }
            int grounded=0,moved=0,uphill=0;var displaced=new List<Tree>();
            try
            {
                // Retain well-spaced trees first; do not flatten the map into an evenly spaced plantation.
                foreach(var tree in trees.OrderBy(t=>rng.Next()))
                {
                    bool valid=Ground(tree.p,out var p,out var elevation);tree.invalidGround=!valid;
                    if(!valid||Nearest(p,tree.radius,out var neighbors)<0||neighbors>=7)
                    {displaced.Add(tree);continue;}
                    if(Mathf.Abs(tree.p.y-p.y)>.15f){tree.transform.position=p;grounded++;}
                    tree.p=p;Register(tree);
                }
                var bounds=new Bounds();bool first=true;
                foreach(var r in generated.Find("Mountains").GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                var river=generated.Find("Water/Water").GetComponent<Renderer>().bounds;
                var sites=new List<Site>();
                for(float x=bounds.min.x+6;x<bounds.max.x-6;x+=6)
                for(float z=river.min.z-12;z<river.max.z+12;z+=6)
                {var point=new Vector3(x+Random(-2.8f,2.8f),0,z+Random(-2.8f,2.8f));if(Ground(point,out var p,out var elevation)&&Nearest(p,2.5f,out var neighbors)>6.3f&&neighbors<7)sites.Add(new Site{p=p,upland=elevation});}
                foreach(var tree in displaced.OrderByDescending(t=>t.invalidGround))
                {
                    int best=-1;float score=float.MinValue;
                    // Fill broad gaps and quieter hillsides, rather than moving a whole clump to one new spot.
                    for(int attempt=0;attempt<Mathf.Min(240,sites.Count);attempt++)
                    {
                        int index=rng.Next(sites.Count);var site=sites[index];float nearest=Nearest(site.p,tree.radius,out var neighbors);if(nearest<0||neighbors>=6)continue;
                        float mountain=site.upland>14&&site.upland<58?5:0;
                        float local=Vector2.Distance(new Vector2(site.p.x,site.p.z),new Vector2(tree.p.x,tree.p.z));
                        float candidate=Mathf.Min(nearest,30)*1.5f-neighbors*2+mountain-local*.008f+Random(0,2);
                        if(candidate>score){score=candidate;best=index;}
                    }
                    if(best<0)
                    {
                        for(int i=0;i<sites.Count;i++)if(Nearest(sites[i].p,tree.radius,out var count)>0&&count<7){best=i;break;}
                    }
                    if(best<0)
                    {
                        float height=float.MinValue;
                        foreach(var hit in Physics.RaycastAll(new Vector3(tree.p.x,240,tree.p.z),Vector3.down,320))if(ground.Contains(hit.collider))height=Mathf.Max(height,hit.point.y);
                        if(height>float.MinValue){tree.p.y=height;tree.transform.position=tree.p;grounded++;}
                        Register(tree);continue;
                    }
                    var target=sites[best];sites.RemoveAt(best);if(target.p.y>tree.p.y+4)uphill++;
                    tree.p=target.p;tree.transform.position=target.p;EditorUtility.SetDirty(tree.transform);Register(tree);moved++;
                }
                LastReport=$"Balanced {trees.Count} trees: relocated {moved}, {uphill} to higher ground, regrounded {grounded}. Retained tree models, sizes and count.";
            }
            finally{foreach(var c in temporary)Object.DestroyImmediate(c);}
            Physics.SyncTransforms();EditorUtility.SetDirty(track);return LastReport;
        }
    }
}

