using System.Collections.Generic;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    public sealed class BoatAIInput : MonoBehaviour, IBoatInput
    {
        public enum Mode { Racing, Avoiding, Overtaking, Recovering, Finished }
        public Mode CurrentMode { get; private set; }
        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public bool ConsumeReset()=>false;
        public float Pace { get; set; }=1;
        public bool DebugSensors;
        readonly List<Vector3> route=new List<Vector3>();
        readonly List<float> routeMeters=new List<float>();
        readonly RaycastHit[] hits=new RaycastHit[24];
        BoatController boat;
        Rigidbody body;
        int segment;
        float slowTime,recoveryTime,thinking,avoidSide=1;
        float furthestRoute;
        float finishLane;
        DifficultyLevel difficulty=DifficultyLevel.Normal;
        public void ConfigureDifficulty(DifficultyLevel level){difficulty=level;Pace=RaceDifficulty.Pace(level);}
        public int RecoveryCount { get; private set; }

        public void Initialize(BoatController controller,int routeChoice)
        {
            routeChoice=BoatCourse.RouteForHull(controller,routeChoice);
            boat=controller;body=GetComponent<Rigidbody>();boat.Input=this;
            finishLane=(routeChoice%5-2)*3;
            var track=boat.Track;float distance=track.StartDistance-22;
            // The first fork bypasses the solid barrier across the main river.
            for(int i=0;i<track.Branches.Count;i++)
            {
                var b=track.Branches[i];
                bool chosen=i==(routeChoice%2)||i==2&&routeChoice%3==0;
                if(!chosen||b.fromDistance<distance)continue;
                // Approach the branch mouth diagonally across the widened river, before the fork.
                // Following the centre until the mouth would require a 90-degree turn at racing speed.
                for(;distance<b.fromDistance-25;distance+=3)route.Add(track.AfterRaceInsertion(track.EvaluateAtDistance(distance).center));
                for(float s=0;s<b.Length;s+=3)route.Add(track.AfterRaceInsertion(b.EvaluateAtDistance(s).center));
                route.Add(track.AfterRaceInsertion(b.EvaluateAtDistance(b.Length).center));distance=b.toDistance+25;
            }
            for(;distance<track.FinishDistance+15;distance+=3)route.Add(track.AfterRaceInsertion(track.EvaluateAtDistance(distance).center));
            routeMeters.Add(0);
            for(int i=1;i<route.Count;i++)routeMeters.Add(routeMeters[i-1]+Vector3.ProjectOnPlane(route[i]-route[i-1],Vector3.up).magnitude);
            boat.Respawned+=RejoinRoute;
        }
        void RejoinRoute()
        {
            segment=0;float best=float.MaxValue;
            for(int i=0;i<route.Count-1;i++)
            {
                float d=(route[i]-transform.position).sqrMagnitude;
                if(d<best){best=d;segment=i;}
            }
            slowTime=recoveryTime=0;furthestRoute=routeMeters[segment];
        }
        void FixedUpdate()
        {
            if(boat==null||route.Count<2||body.isKinematic)return;
            var progress=GetComponent<RaceProgress>();
            if(progress!=null&&progress.Finished)
            {
                // Clear the finish gate before parking, so later racers are not blocked by winners.
                CurrentMode=Mode.Finished;Throttle=.45f;
                int hint=-1;var water=boat.Track.SampleWater(transform.position,ref hint);
                var finish=boat.Track.EvaluateAtDistance(boat.Track.FinishDistance+9);
                var goal=boat.Track.AfterRaceInsertion(finish.center)+finish.right*finishLane;
                Steer=Mathf.Clamp(Vector3.SignedAngle(transform.forward,Vector3.ProjectOnPlane(goal-transform.position,Vector3.up),Vector3.up)*.04f,-1,1);
                if(water.distanceAlong>boat.Track.FinishDistance+6)
                {
                    Throttle=Steer=0;body.isKinematic=true;
                    foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
                }
                return;
            }
            float dt=Time.fixedDeltaTime;
            thinking-=dt;if(thinking>0)return;thinking=RaceDifficulty.Reaction(difficulty);
            float best=float.MaxValue;int nearest=segment;float fraction=0;
            for(int i=Mathf.Max(0,segment-3);i<Mathf.Min(route.Count-1,segment+35);i++)
            {
                var delta=route[i+1]-route[i];delta.y=0;
                var offset=transform.position-route[i];offset.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                float d=(offset-delta*t).sqrMagnitude;
                if(d<best){best=d;nearest=i;fraction=t;}
            }
            segment=nearest;
            Vector3 target=Vector3.Lerp(route[segment],route[segment+1],fraction);
            float ahead=Mathf.Clamp(boat.Speed*.8f+5,6,14);
            for(int i=segment+1;i<route.Count;i++)
            {
                float length=Vector3.Distance(target,route[i]);
                if(length>=ahead){target=Vector3.MoveTowards(target,route[i],ahead);break;}
                ahead-=length;target=route[i];
            }
            var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            var desired=Vector3.ProjectOnPlane(target-transform.position,Vector3.up).normalized;
            float angle=Vector3.SignedAngle(forward,desired,Vector3.up);
            float navigation=Mathf.Clamp(angle*.035f,-1,1);
            float left=Clearance(Quaternion.AngleAxis(-32,Vector3.up)*forward);
            float center=Clearance(forward);
            float right=Clearance(Quaternion.AngleAxis(32,Vector3.up)*forward);
            float range=Mathf.Clamp(boat.Speed*.65f+4,5,12);
            CurrentMode=Mode.Racing;
            if(center<range||Mathf.Min(left,right)<2)
            {
                if(Mathf.Abs(right-left+navigation*3)>.4f)avoidSide=right-left+navigation*3>0?1:-1;
                navigation=Mathf.Clamp(navigation+avoidSide*Mathf.InverseLerp(range,1,center)*.6f,-1,1);
                CurrentMode=Mode.Avoiding;
                if(PhysicsBoatAhead(forward,range))CurrentMode=Mode.Overtaking;
            }
            Steer=navigation;
            Throttle=Mathf.Clamp(Pace,.3f,1f)*Mathf.Lerp(1,.4f,Mathf.Clamp01(Mathf.Abs(angle)/100));
            float course=RaceProgress.CourseDistance(boat.Track,boat.Water);
            foreach(var branch in boat.Track.Branches)
                if(course>branch.fromDistance-35&&course<branch.fromDistance+35)
                    Throttle=Mathf.Min(Throttle,Mathf.Clamp((RaceDifficulty.JunctionSpeed(difficulty)-boat.Speed)*.4f+.35f,-.35f,.65f));
            if(center<2)Throttle=.25f;
            float routePosition=Mathf.Lerp(routeMeters[segment],routeMeters[segment+1],fraction);
            if(routePosition>furthestRoute+.8f){furthestRoute=routePosition;slowTime=0;}
            else slowTime+=thinking;
            if(slowTime>2.5f&&recoveryTime<=0)
            {
                recoveryTime=1.6f;RecoveryCount++;
                if(RecoveryCount%3==0){boat.Respawn();return;}
            }
            if(recoveryTime>0)
            {
                CurrentMode=Mode.Recovering;recoveryTime-=thinking;
                Throttle=-.8f;Steer=-avoidSide;
                if(recoveryTime<=0){slowTime=0;furthestRoute=routePosition;}
            }
            if(DebugSensors)Debug.DrawLine(transform.position,target,Color.yellow,.11f);
        }
        float Clearance(Vector3 direction)
        {
            float range=Mathf.Clamp(boat.Speed*.65f+4,5,12);
            var origin=body.position+Vector3.up*.1f;
            int count=Physics.SphereCastNonAlloc(origin,Mathf.Min(.5f,boat.hullBeam*.3f),direction,hits,range,~0,QueryTriggerInteraction.Ignore);
            float closest=range;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(hit.collider.transform.IsChildOf(transform))continue;
                var learning=GetComponent<BoatLearningAgent>();var other=hit.collider.GetComponentInParent<BoatLearningAgent>();
                if(learning!=null&&learning.Training&&other!=null&&other.Training)continue;
                // Water surfaces and the river bed must not look like a wall.
                if(hit.normal.y>.75f||hit.point.y<boat.Water.height-.25f)continue;
                closest=Mathf.Min(closest,hit.distance);
            }
            if(DebugSensors)Debug.DrawRay(origin,direction*closest,closest<range?Color.red:Color.green,.11f);
            return closest;
        }
        bool PhysicsBoatAhead(Vector3 direction,float range)
        {
            int count=Physics.RaycastNonAlloc(body.position+Vector3.up*.2f,direction,hits,range,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var other=hits[i].collider.GetComponentInParent<BoatController>();if(other==null||other==boat)continue;
                var ownAgent=GetComponent<BoatLearningAgent>();var otherAgent=other.GetComponent<BoatLearningAgent>();
                if(ownAgent!=null&&ownAgent.Training&&otherAgent!=null&&otherAgent.Training)continue;
                return true;
            }
            return false;
        }
        public void Stop(){Steer=Throttle=0;CurrentMode=Mode.Finished;}
        public void CrossedFinish()
        {
            CurrentMode=Mode.Finished;
            boat.RecoveryEnabled=false;
        }
        void OnDestroy(){if(boat!=null)boat.Respawned-=RejoinRoute;}
    }
}
