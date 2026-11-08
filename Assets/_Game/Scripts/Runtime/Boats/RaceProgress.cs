using System.Collections.Generic;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    public sealed class RaceProgress : MonoBehaviour
    {
        public BoatController Boat { get; private set; }
        public string RacerName { get; private set; }
        public float Distance { get; private set; }
        public int Checkpoints { get; private set; }
        /// <summary>Thời điểm qua từng cổng, mili giây. Mốc cuối chính là thời gian về đích.</summary>
        public readonly List<int> CheckpointTimesMs = new List<int>();
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        public int FinishOrder { get; set; }
        public bool Withdrawn { get; private set; }
        public bool TimedOut {get;private set;}
        public void MarkTimedOut(){TimedOut=true;Withdraw();}
        public void Withdraw(){Withdrawn=true;Boat.GetComponent<Rigidbody>().isKinematic=true;foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;}
        public void ApplyNetwork(BoatNetworkState state)
        {
            Distance=state.distance;Checkpoints=state.checkpoints;Finished=state.finished;
            FinishTime=state.finishTime;FinishOrder=state.finishOrder;Withdrawn=state.withdrawn;
        }
        RiverTrack track;
        Vector3 previous, safePosition;
        Quaternion safeRotation;
        float nextGate, safeDistance, lastDistance;
        int hint=-1;
        public const float GateSpacing=80;

        public void Initialize(BoatController boat,string racerName)
        {
            Boat=boat;track=boat.Track;RacerName=racerName;
            previous=safePosition=boat.transform.position;safeRotation=boat.transform.rotation;
            Distance=safeDistance=lastDistance=track.StartDistance-12;nextGate=track.StartDistance;
            boat.Respawned+=RestoreCheckpoint;
        }
        public static float CourseDistance(RiverTrack track,WaterInfo water)
        {
            if(water.channel<0)return water.distanceAlong;
            var b=track.Branches[water.channel];
            return Mathf.Lerp(b.fromDistance,b.toDistance,water.distanceAlong/Mathf.Max(1,b.Length));
        }
        public void Tick(float elapsed)
        {
            if(Finished||Withdrawn||Boat==null)return;
            var position=Boat.transform.position;
            var w=track.SampleWater(position,ref hint);
            float d=CourseDistance(track,w);
            float previousDistance=lastDistance;
            bool continuous=Vector3.Distance(position,previous)<30&&Mathf.Abs(d-lastDistance)<35;
            previous=position;lastDistance=d;
            float clearance=position.y-w.height-Boat.FloatHeight;
            if(!continuous||!w.inWater||Boat.transform.up.y<.3f||clearance>3||clearance<-.6f)return;
            // Ordered gates prevent a jump to the finish from completing a race.
            if(d>=nextGate&&previousDistance<nextGate&&d-nextGate<30)
            {
                Checkpoints++;CheckpointTimesMs.Add(Mathf.RoundToInt(elapsed*1000));safeDistance=nextGate;
                safePosition=w.center+Vector3.up*Boat.FloatHeight;
                safeRotation=Quaternion.LookRotation(w.tangent,Vector3.up);
                nextGate=Mathf.Min(track.FinishDistance,nextGate+GateSpacing);
                if(safeDistance>=track.FinishDistance)
                {
                    Finished=true;FinishTime=elapsed;Distance=track.FinishDistance;return;
                }
            }
            Distance=Mathf.Clamp(d,track.StartDistance-12,nextGate);
        }
        void RestoreCheckpoint()
        {
            if(Finished)return;
            Boat.Place(safePosition,safeRotation);previous=safePosition;lastDistance=safeDistance;
            Distance=safeDistance;hint=-1;
        }
        void OnDestroy(){if(Boat!=null)Boat.Respawned-=RestoreCheckpoint;}
    }
}
