using System.Collections.Generic;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    // Offline race authority. Visuals are pooled; collection/hazards are distance based.
    public sealed class RaceItems : MonoBehaviour
    {
        sealed class Pickup {public GameObject view;public Vector3 position;public RaceItem item;public float cooldown;}
        sealed class Log {public GameObject view;public BoatController owner;public Vector3 position,right;public float life;public readonly HashSet<BoatController> hit=new HashSet<BoatController>();}
        readonly List<Pickup> pickups=new List<Pickup>();
        readonly List<Log> logs=new List<Log>();
        readonly List<Material> materials=new List<Material>();
        BoatRace race;RiverTrack track;float age,aiTimer;
        readonly RaycastHit[] hits=new RaycastHit[32];
        public Material ShieldMaterial {get;private set;}
        public bool CanUse=>race!=null&&race.Running&&race.CountdownRemaining<=0;
        public int PickupCount=>pickups.Count;
        public int Seed {get;private set;}
        public int PickupSignature {get;private set;}
        public int ActiveLogs {get{int n=0;foreach(var log in logs)if(log.life>0)n++;return n;}}
        Material MakeMaterial(Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
            var material=new Material(shader);material.color=color;material.enableInstancing=true;materials.Add(material);return material;
        }
        GameObject Shape(PrimitiveType shape,Transform parent,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.transform.SetParent(parent,false);go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return go;
        }
        public void Initialize(BoatRace controller,RiverTrack river,int seed=0)
        {
            race=controller;track=river;Seed=seed;var random=new System.Random(seed);
            ShieldMaterial=MakeMaterial(new Color(.1f,.8f,1));var wood=MakeMaterial(new Color(.55f,.3f,.12f));
            foreach(var racer in race.Racers){var state=racer.gameObject.AddComponent<BoatItemState>();state.Arena=this;}
            // Both first branches and the optional upper branch are covered by these routes.
            for(int route=0;route<4;route++)
            {
                var course=new BoatCourse(track,route);
                for(float distance=35+(float)random.NextDouble()*8;distance<course.Length-35;distance+=18+(float)random.NextDouble()*6)
                {
                    for(int lane=-1;lane<=1;lane++)
                    {
                    if(pickups.Count>=240)break;
                    var p=course.At(distance+(float)(random.NextDouble()*4-2),out var tangent);
                    p+=Vector3.Cross(Vector3.up,tangent)*(lane*3+(float)(random.NextDouble()*1.4-.7));
                    int hint=-1;var water=track.SampleWater(p,ref hint);
                    if(!water.inWater)continue;p.y=water.height+.8f;
                    bool duplicate=false;foreach(var old in pickups)if(Vector3.Distance(old.position,p)<3.8f){duplicate=true;break;}
                    if(duplicate)continue;
                    int count=Physics.OverlapSphereNonAlloc(p,.7f,overlaps,~0,QueryTriggerInteraction.Ignore);
                    bool blocked=false;for(int i=0;i<count;i++)if(overlaps[i].GetComponentInParent<BoatController>()==null){blocked=true;break;}
                    if(blocked)continue;
                    var item=(RaceItem)random.Next(1,4);
                    var view=new GameObject("Pickup_"+item);view.transform.SetParent(transform,false);view.transform.position=p;
                    view.AddComponent<ItemPickupVisual>().Initialize(item);
                    pickups.Add(new Pickup{view=view,position=p,item=item});
                    unchecked{PickupSignature=PickupSignature*31+Mathf.RoundToInt(p.x*100);PickupSignature=PickupSignature*31+Mathf.RoundToInt(p.z*100);PickupSignature=PickupSignature*31+(int)item;}
                    }
                }
            }
            for(int i=0;i<24;i++)
            {
                var go=Shape(PrimitiveType.Cylinder,transform,new Vector3(.42f,1.5f,.42f),wood);go.name="DroppedLog";go.SetActive(false);
                logs.Add(new Log{view=go});
            }
        }
        readonly Collider[] overlaps=new Collider[24];
        public bool DropLog(BoatController owner)
        {
            Log free=null;foreach(var log in logs)if(log.life<=0){free=log;break;}if(free==null)return false;
            var p=owner.transform.position-owner.transform.forward*(owner.hullLength*.5f+2.2f);int hint=-1;var w=track.SampleWater(p,ref hint);if(!w.inWater)return false;
            p.y=w.height+.16f;free.owner=owner;free.position=p;free.right=Vector3.ProjectOnPlane(owner.transform.right,Vector3.up).normalized;free.life=8;free.hit.Clear();
            free.view.transform.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,free.right));free.view.SetActive(true);return true;
        }
        bool SafeToBoost(BoatController boat)
        {
            int hint=-1;var w=track.SampleWater(boat.transform.position,ref hint);
            if(w.channel>=0)return false;float d=RaceProgress.CourseDistance(track,w);
            foreach(var branch in track.Branches)if(Mathf.Abs(d-branch.fromDistance)<45)return false;
            foreach(var fall in track.falls)if(fall!=null&&Mathf.Abs(d-fall.distance)<35+fall.length)return false;
            if(Vector3.Angle(boat.transform.forward,w.tangent)>12)return false;
            int count=Physics.SphereCastNonAlloc(boat.transform.position+Vector3.up*.15f,.4f,boat.transform.forward,hits,12,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(boat.transform)&&hits[i].normal.y<.75f&&hits[i].point.y>w.height-.25f)return false;
            return true;
        }
        public void Tick(float dt)
        {
            if(!CanUse)return;age+=dt;aiTimer-=dt;bool think=aiTimer<=0;if(think)aiTimer=.5f;
            foreach(var pickup in pickups)
            {
                pickup.cooldown=Mathf.Max(0,pickup.cooldown-dt);pickup.view.SetActive(pickup.cooldown<=0);
                if(pickup.cooldown>0)continue;
                pickup.view.transform.position=pickup.position+Vector3.up*(Mathf.Sin(age*2+pickup.position.z)*.12f);
                foreach(var racer in race.Racers)
                {
                    if(racer.Finished||racer.Withdrawn)continue;
                    var delta=racer.transform.position-pickup.position;
                    if(Mathf.Abs(delta.y)>2.5f)continue;delta.y=0;
                    if(delta.sqrMagnitude<Mathf.Pow(2+racer.Boat.hullBeam*.5f,2)&&racer.GetComponent<BoatItemState>().Give(pickup.item))
                    {pickup.cooldown=5;pickup.view.SetActive(false);break;}
                }
            }
            foreach(var racer in race.Racers)
            {
                var state=racer.GetComponent<BoatItemState>();state.Tick(dt);
                if(racer.Finished||racer.Withdrawn){state.ClearEffects();continue;}
                if(!think||racer==race.Player)continue;
                if(state.Held==RaceItem.Boost&&SafeToBoost(racer.Boat))state.Use();
                else if(state.Held==RaceItem.Shield)state.Use();
                else if(state.Held==RaceItem.Log)
                    foreach(var other in race.Racers)if(other!=racer&&!other.Finished&&!other.Withdrawn)
                    {
                        var offset=other.transform.position-racer.transform.position;
                        if(offset.sqrMagnitude<144&&Vector3.Dot(offset,racer.transform.forward)<-2){state.Use();break;}
                    }
            }
            foreach(var log in logs)
            {
                if(log.life<=0)continue;log.life-=dt;
                if(log.life<=0){log.view.SetActive(false);continue;}
                int hint=-1;var water=track.SampleWater(log.position,ref hint);log.position+=water.flow*(dt*.25f);log.position.y=water.height+.16f;log.view.transform.position=log.position;
                foreach(var racer in race.Racers)
                {
                    var boat=racer.Boat;if(boat==log.owner||racer.Finished||racer.Withdrawn||log.hit.Contains(boat))continue;
                    var delta=boat.transform.position-log.position;if(Mathf.Abs(delta.y)>2)continue;delta.y=0;
                    var closest=log.right*Mathf.Clamp(Vector3.Dot(delta,log.right),-1.5f,1.5f);
                    if((delta-closest).sqrMagnitude>Mathf.Pow(boat.hullBeam*.5f+.6f,2))continue;
                    log.hit.Add(boat);boat.ApplyItemHit();
                }
            }
        }
        void OnDestroy()
        {
            foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
