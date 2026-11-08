using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;

namespace VuotThac.Boats
{
    public sealed class BoatLearningAgent : Agent,IBoatInput
    {
        public const int ObservationCount=42;
        public const int ObservationSchema=4;
        public const string Behavior="SuoiRungBoat";
        public bool Training { get; private set; }
        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public bool ConsumeReset()=>false;
        public float CourseDistance { get; private set; }
        public int Episodes { get; private set; }
        public int Successes { get; private set; }
        public int EpisodeCollisions { get; private set; }
        public float LastEpisodeProgress { get; private set; }
        public BoatCourse Course { get; private set; }
        BoatController boat;
        Rigidbody body;
        BoatAIInput teacher;
        System.Random random;
        readonly RaycastHit[] hits=new RaycastHit[48];
        readonly Collider[] overlap=new Collider[48];
        static readonly float[] RayAngles={-100,-70,-45,-22,0,22,45,70,100};
        int segment,hint=-1;
        float lateral,previousDistance,startDistance,goalDistance,elapsed,stall,furthest,pace=1;
        float navigationAngle,frontClearance=12;
        bool openMain;
        bool ending;

        public static BoatLearningAgent Attach(BoatController boat,int choice,bool training,ModelAsset model=null,bool demonstration=false,int seed=1)
        {
            choice=BoatCourse.RouteForHull(boat,choice);
            var behavior=boat.gameObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName=Behavior;behavior.BrainParameters.VectorObservationSize=ObservationCount;
            behavior.BrainParameters.ActionSpec=ActionSpec.MakeContinuous(2);
            behavior.InferenceDevice=InferenceDevice.Burst;behavior.DeterministicInference=true;behavior.Model=model;
            behavior.BehaviorType=demonstration?BehaviorType.HeuristicOnly:training?BehaviorType.Default:BehaviorType.InferenceOnly;
            BoatAIInput teacher=null;
            if(demonstration){teacher=boat.gameObject.AddComponent<BoatAIInput>();teacher.Initialize(boat,choice);teacher.ConfigureDifficulty(DifficultyLevel.Normal);}
            var agent=boat.gameObject.AddComponent<BoatLearningAgent>();
            agent.Configure(boat,choice,training,teacher,seed);
            var decision=boat.gameObject.AddComponent<DecisionRequester>();decision.DecisionPeriod=5;decision.TakeActionsBetweenDecisions=true;
            return agent;
        }
        public override void Initialize()
        {
            boat=GetComponent<BoatController>();body=GetComponent<Rigidbody>();boat.Input=this;
            boat.Impact+=Impact;boat.Respawned+=UnexpectedRespawn;MaxStep=0;
        }
        void Configure(BoatController controller,int choice,bool training,BoatAIInput expert,int seed)
        {
            boat=controller;body=GetComponent<Rigidbody>();Training=training;teacher=expert;random=new System.Random(seed);
            Course=new BoatCourse(boat.Track,choice);boat.Input=this;
            if(training)boat.RecoveryEnabled=false;
            CourseDistance=Course.Nearest(transform.position,ref segment,out lateral,true);
            previousDistance=CourseDistance;
        }
        public void SetDifficulty(DifficultyLevel difficulty)
        {
            pace=RaceDifficulty.Pace(difficulty)/RaceDifficulty.Pace(DifficultyLevel.Normal);
            // Keep the cadence learned by this policy; pacing changes only on open straights.
            GetComponent<DecisionRequester>().DecisionPeriod=5;
        }
        public override void OnEpisodeBegin()
        {
            if(Course==null)return;
            ending=false;elapsed=stall=0;EpisodeCollisions=0;Steer=Throttle=0;
            if(!Training){CourseDistance=Course.Nearest(transform.position,ref segment,out lateral,true);previousDistance=CourseDistance;return;}
            float horizon=teacher!=null?1400:Academy.Instance.EnvironmentParameters.GetWithDefault("episode_length",90);
            for(int attempt=0;attempt<40;attempt++)
            {
                startDistance=(float)random.NextDouble()*Mathf.Max(1,Course.Length-horizon-15);
                if(horizon>Course.Length*.8f||random.NextDouble()<.25)startDistance=10;
                var position=Course.At(startDistance,out var forward);
                int waterHint=-1;var water=boat.Track.SampleWater(position,ref waterHint);
                if(!water.inWater)continue;
                float spread=water.channel<0?12.5f:Mathf.Min(3,boat.Track.Branches[water.channel].HalfWidth*.45f);
                position+=Vector3.Cross(Vector3.up,forward)*(float)(random.NextDouble()*2-1)*spread;
                waterHint=-1;water=boat.Track.SampleWater(position,ref waterHint);
                if(!water.inWater)continue;
                position.y=water.height+boat.FloatHeight+.03f;
                var rotation=Quaternion.AngleAxis((float)(random.NextDouble()*30-15),Vector3.up)*Quaternion.LookRotation(forward);
                if(!SpawnClear(position,rotation)&&attempt<39)continue;
                boat.Place(position,rotation);body.linearVelocity=forward*(1+(float)random.NextDouble()*3);break;
            }
            goalDistance=Mathf.Min(Course.Length-12,startDistance+horizon);
            CourseDistance=Course.Nearest(transform.position,ref segment,out lateral,true);
            startDistance=previousDistance=furthest=CourseDistance;
            if(teacher!=null)teacher.SendMessage("RejoinRoute");
        }
        bool SpawnClear(Vector3 p,Quaternion q)
        {
            int count=Physics.OverlapBoxNonAlloc(p,new Vector3(boat.hullBeam*.45f,boat.hullDepth*.4f,boat.hullLength*.45f),overlap,q,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=overlap[i];if(c.transform.IsChildOf(transform))continue;
                var agent=c.GetComponentInParent<BoatLearningAgent>();if(agent!=null&&agent.Training)continue;
                return false;
            }
            return true;
        }
        public override void CollectObservations(VectorSensor sensor)
        {
            if(Course==null){for(int i=0;i<ObservationCount;i++)sensor.AddObservation(0f);return;}
            CourseDistance=Course.Nearest(transform.position,ref segment,out lateral);
            var water=boat.Track.SampleWater(transform.position,ref hint);
            var velocity=transform.InverseTransformDirection(body.linearVelocity);
            sensor.AddObservation(velocity.x/20);sensor.AddObservation(velocity.z/20);sensor.AddObservation(body.angularVelocity.y/3);
            for(int lookIndex=0;lookIndex<3;lookIndex++)
            {
                float ahead=6f*(1<<lookIndex);var dir=Course.At(CourseDistance+ahead,out _)-transform.position;dir.y=0;dir=transform.InverseTransformDirection(dir.normalized);sensor.AddObservation(dir.x);sensor.AddObservation(dir.z);}
            float half=water.channel<0?boat.Track.HalfWidth+boat.Track.appliedRaceStripWidth*.5f:boat.Track.Branches[water.channel].HalfWidth;
            sensor.AddObservation(Mathf.Clamp(lateral/Mathf.Max(1,half),-2,2));sensor.AddObservation(Mathf.Clamp01((Course.Length-CourseDistance)/100));
            sensor.AddObservation(transform.up);var flow=transform.InverseTransformDirection(water.flow);sensor.AddObservation(flow.x/10);sensor.AddObservation(flow.z/10);
            sensor.AddObservation(water.inWater?1f:0);sensor.AddObservation(Mathf.Clamp((transform.position.y-water.height-boat.FloatHeight)/3,-2,2));
            // Do not expose previous commands: imitation can learn an identity shortcut
            // with a tiny offline loss while producing no useful closed-loop control.
            var target=Course.At(CourseDistance+Mathf.Clamp(boat.Speed*.8f+5,6,14),out _)-transform.position;target.y=0;
            navigationAngle=Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward,Vector3.up),target,Vector3.up);
            sensor.AddObservation(navigationAngle/180);
            float targetSpeed=boat.definition.MaxSpeed;
            float routeDistance=RaceProgress.CourseDistance(boat.Track,water);
            foreach(var branch in boat.Track.Branches)
                if(routeDistance>branch.fromDistance-35&&routeDistance<branch.fromDistance+35)targetSpeed=5;
            openMain=water.channel<0&&routeDistance>450&&targetSpeed>5;
            sensor.AddObservation(targetSpeed/20);
            sensor.AddObservation(boat.hullLength/8);sensor.AddObservation(boat.hullBeam/3);
            sensor.AddObservation(boat.definition.MaxSpeed/20);sensor.AddObservation(boat.definition.Grip/5);
            frontClearance=12;
            foreach(float angle in RayAngles)
            {
                var direction=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
                int count=Physics.SphereCastNonAlloc(body.position+Vector3.up*.1f,Mathf.Min(.4f,boat.hullBeam*.3f),direction,hits,12,~0,QueryTriggerInteraction.Ignore);
                float nearest=12;bool otherBoat=false;
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];if(hit.collider.transform.IsChildOf(transform)||hit.normal.y>.75f||hit.point.y<water.height-.25f)continue;
                    var other=hit.collider.GetComponentInParent<BoatLearningAgent>();if(Training&&other!=null&&other.Training)continue;
                    if(hit.distance<nearest){nearest=hit.distance;otherBoat=hit.collider.GetComponentInParent<BoatController>()!=null;}
                }
                sensor.AddObservation(nearest/12);sensor.AddObservation(otherBoat?1f:0);
                if(Mathf.Abs(angle)<=22)frontClearance=Mathf.Min(frontClearance,nearest);
            }
        }
        public override void OnActionReceived(ActionBuffers actions)
        {
            Steer=Mathf.Clamp(actions.ContinuousActions[0],-1,1);
            float power=actions.ContinuousActions[1];
            bool adjustPace=!Training&&openMain&&Mathf.Abs(navigationAngle)<10&&frontClearance>10&&power>.35f;
            Throttle=Mathf.Clamp(power*(adjustPace?pace:1),-1,1);
            if(!Training)
            {
                var progress=GetComponent<RaceProgress>();
                if(progress!=null&&progress.Finished)
                {
                    Steer=0;Throttle=.35f;
                    int finishHint=-1;var finishWater=boat.Track.SampleWater(transform.position,ref finishHint);
                    if(RaceProgress.CourseDistance(boat.Track,finishWater)>boat.Track.FinishDistance+6)
                    {
                        Throttle=0;body.isKinematic=true;
                        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
                    }
                }
                else if(!body.isKinematic&&Course!=null)
                {
                    // Shared checkpoint recovery can miss a boat circling in place:
                    // yaw motion is intentional for players, but an AI must make progress.
                    if(CourseDistance>furthest+.8f){furthest=CourseDistance;stall=0;}
                    else stall+=Time.fixedDeltaTime;
                    if(stall>8){stall=0;boat.Respawn();}
                }
                return;
            }
            if(Course==null||ending)return;
            float dt=Time.fixedDeltaTime;elapsed+=dt;
            CourseDistance=Course.Nearest(transform.position,ref segment,out lateral);
            float delta=Mathf.Clamp(CourseDistance-previousDistance,-2,2);previousDistance=CourseDistance;
            AddReward(delta/20-.015f*dt);
            var water=boat.Track.SampleWater(transform.position,ref hint);
            if(water.edgeDistance>-.5f)AddReward(-.1f*dt);
            if(CourseDistance>furthest+.8f){furthest=CourseDistance;stall=0;}else stall+=dt;
            if(CourseDistance>=goalDistance){FinishEpisode(true);return;}
            if(!water.inWater&&water.edgeDistance>2||transform.up.y<.3f||Mathf.Abs(transform.position.y-water.height)>8||stall>6||elapsed>(goalDistance-startDistance)/2+15)
                FinishEpisode(false);
        }
        void FinishEpisode(bool success)
        {
            if(ending)return;ending=true;Episodes++;if(success)Successes++;
            LastEpisodeProgress=Mathf.Clamp01((CourseDistance-startDistance)/Mathf.Max(1,goalDistance-startDistance));
            AddReward(success?1:-1);
            Academy.Instance.StatsRecorder.Add("Boat/Success",success?1:0);
            Academy.Instance.StatsRecorder.Add("Boat/Progress",LastEpisodeProgress);
            Academy.Instance.StatsRecorder.Add("Boat/Collisions",EpisodeCollisions);
            EndEpisode();
        }
        void Impact(float speed){EpisodeCollisions++;if(Training)AddReward(-Mathf.Min(.5f,speed*.04f));}
        void UnexpectedRespawn()
        {
            if(Training)FinishEpisode(false);
            else if(Course!=null)
            {
                CourseDistance=Course.Nearest(transform.position,ref segment,out lateral,true);
                previousDistance=furthest=CourseDistance;stall=0;
            }
        }
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions=actionsOut.ContinuousActions;
            actions[0]=teacher!=null?teacher.Steer:0;
            actions[1]=teacher!=null?teacher.Throttle:0;
        }
        protected override void OnDisable()
        {base.OnDisable();}
        void OnDestroy(){if(boat!=null){boat.Impact-=Impact;boat.Respawned-=UnexpectedRespawn;}}
    }
}
