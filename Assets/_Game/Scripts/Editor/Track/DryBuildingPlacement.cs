using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
using VuotThac.World;

namespace VuotThac.EditorTools.Track
{
    public static class DryBuildingPlacement
    {
        public static string LastReport { get; private set; }
        public static void Repair(RiverTrack track)
        {
            Physics.SyncTransforms();
            var root=track.transform.Find("Generated");
            var terrain=root.Find("Terrain");var mountains=root.Find("Mountains");
            var houses=new List<MeshFilter>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh!=null&&(filter.sharedMesh.name=="Cabin"||filter.sharedMesh.name=="HouseTall"))houses.Add(filter);
            var flowers=new Dictionary<Transform,Transform>();
            var gardens=root.Find("Gardens");
            if(gardens!=null)foreach(Transform plant in gardens)
            {
                if(plant.name!="CottageFlowerBed")continue;
                float best=float.MaxValue;Transform owner=null;
                foreach(var house in houses){float d=(plant.position-house.transform.position).sqrMagnitude;if(d<best){best=d;owner=house.transform;}}
                if(best<144)flowers[plant]=owner;
            }
            bool Ground(Vector3 p,out float height)
            {
                height=float.MinValue;
                foreach(var hit in Physics.RaycastAll(new Vector3(p.x,250,p.z),Vector3.down,500,~0,QueryTriggerInteraction.Ignore))
                    if(hit.transform.IsChildOf(terrain)||(mountains!=null&&hit.transform.IsChildOf(mountains)))height=Mathf.Max(height,hit.point.y);
                return height>float.MinValue;
            }
            bool Dry(Vector3 center,Vector3 extents,out float high,out float low)
            {
                high=float.MinValue;low=float.MaxValue;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    var p=center+new Vector3(x*(extents.x+0.65f),0,z*(extents.z+0.65f));
                    int hint=-1;var water=track.SampleWater(p,ref hint);
                    if(water.inWater||water.edgeDistance<0.8f)return false;
                    if(!Ground(p,out float ground)||ground<water.height+0.35f)return false;
                    high=Mathf.Max(high,ground);low=Mathf.Min(low,ground);
                }
                return true;
            }
            int moved=0,unresolved=0;string report="";
            for(int index=0;index<houses.Count;index++)
            {
                var house=houses[index];var bounds=house.GetComponent<Renderer>().bounds;
                bool wet=false;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    var point=new Vector3(bounds.center.x+x*bounds.extents.x,0,bounds.center.z+z*bounds.extents.z);
                    int hint=-1;if(track.SampleWater(point,ref hint).inWater)wet=true;
                }
                if(!wet)continue;
                var old=house.transform.position;var best=old;float score=float.MaxValue;
                float side=Mathf.Sign(old.x-track.CenterXAtMapZ(old.z));
                for(float radius=2;radius<=76;radius+=2)
                {
                    for(int direction=0;direction<24;direction++)
                    {
                        float angle=direction*Mathf.PI*2/24;
                        var p=old+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                        if(Mathf.Sign(p.x-track.CenterXAtMapZ(p.z))!=side)continue;
                        var footprint=new Vector3(p.x+bounds.center.x-old.x,0,p.z+bounds.center.z-old.z);
                        if(!Dry(footprint,bounds.extents,out float high,out float low)||high-low>0.85f)continue;
                        if(high<old.y-0.2f)continue;
                        bool occupied=false;
                        foreach(var other in houses)
                            if(other!=house){var delta=other.transform.position-p;delta.y=0;if(delta.magnitude<10){occupied=true;break;}}
                        if(occupied)continue;
                        foreach(string groupName in new[]{"Trees","DeepForest","MountainForest"})
                        {
                            var trees=root.Find(groupName);if(trees==null)continue;
                            foreach(Transform tree in trees)
                                if(Mathf.Abs(tree.position.x-p.x)<bounds.extents.x+1&&Mathf.Abs(tree.position.z-p.z)<bounds.extents.z+1){occupied=true;break;}
                            if(occupied)break;
                        }
                        if(occupied)continue;
                        float cost=radius+Mathf.Abs(high-old.y)*0.7f;
                        if(cost<score){score=cost;best=new Vector3(p.x,high+0.03f,p.z);}
                    }
                    if(score<float.MaxValue&&radius>score+4)break;
                }
                if(score==float.MaxValue){unresolved++;report+=house.name+": no dry site; ";continue;}
                var shift=best-old;house.transform.position=best;
                var courtyard=root.Find("VillageFinish/CottageCourtyard_"+index);
                if(courtyard!=null)
                {
                    courtyard.position+=shift;
                    foreach(Transform item in courtyard)
                        if(Ground(item.position,out float ground))
                        {
                            var p=item.position;p.y=ground+(item.name=="StonePath"?0.025f:0);item.position=p;
                        }
                }
                foreach(var pair in flowers)if(pair.Value==house.transform)
                {
                    pair.Key.position+=shift;
                    if(Ground(pair.Key.position,out float ground)){var p=pair.Key.position;p.y=ground-0.02f;pair.Key.position=p;}
                }
                foreach(var actor in root.GetComponentsInChildren<RealisticVillager>())
                {
                    var delta=actor.routeA-old;delta.y=0;if(delta.magnitude>7)continue;
                    actor.transform.position+=shift;actor.routeA+=shift;actor.routeB+=shift;
                    if(Ground(actor.routeA,out float a))actor.routeA=new Vector3(actor.routeA.x,a,actor.routeA.z);
                    if(Ground(actor.routeB,out float b))actor.routeB=new Vector3(actor.routeB.x,b,actor.routeB.z);
                    EditorUtility.SetDirty(actor);
                }
                moved++;report+=house.name+": "+old+" -> "+best+"; ";
            }
            Physics.SyncTransforms();
            LastReport=$"Relocated {moved}, unresolved {unresolved}. "+report;
        }
    }
}
