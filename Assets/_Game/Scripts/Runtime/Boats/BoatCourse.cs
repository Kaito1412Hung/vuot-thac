using System.Collections.Generic;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    // Navigation goals only. This class never computes steering or propulsion.
    public sealed class BoatCourse
    {
        public static int RouteForHull(BoatController boat,int choice)
        {
            // Long or fast hulls with limited steering use the gentler fork entrance.
            bool wideTurn=(boat.hullLength>5.5f||boat.definition.MaxSpeed>13)&&boat.definition.TurnRate<85;
            return boat.Track.Branches.Count>1&&wideTurn?choice|1:choice;
        }
        public readonly List<Vector3> Points=new List<Vector3>();
        public readonly List<float> Meters=new List<float>();
        public float Length=>Meters[Meters.Count-1];
        public BoatCourse(RiverTrack track,int choice)
        {
            float d=track.StartDistance-22;
            for(int i=0;i<track.Branches.Count;i++)
            {
                var b=track.Branches[i];
                if(!(i==choice%2||i==2&&choice%3==0)||b.fromDistance<d)continue;
                for(;d<b.fromDistance-25;d+=3)Points.Add(track.AfterRaceInsertion(track.EvaluateAtDistance(d).center));
                for(float s=0;s<b.Length;s+=3)Points.Add(track.AfterRaceInsertion(b.EvaluateAtDistance(s).center));
                Points.Add(track.AfterRaceInsertion(b.EvaluateAtDistance(b.Length).center));d=b.toDistance+25;
            }
            for(;d<track.FinishDistance+15;d+=3)Points.Add(track.AfterRaceInsertion(track.EvaluateAtDistance(d).center));
            Meters.Add(0);for(int i=1;i<Points.Count;i++)Meters.Add(Meters[i-1]+Flat(Points[i]-Points[i-1]).magnitude);
        }
        static Vector3 Flat(Vector3 p){p.y=0;return p;}
        public float Nearest(Vector3 point,ref int segment,out float lateral,bool full=false)
        {
            int start=full?0:Mathf.Max(0,segment-4),end=full?Points.Count-1:Mathf.Min(Points.Count-1,segment+40);
            float best=float.MaxValue,fraction=0;int nearest=segment;
            for(int i=start;i<end;i++)
            {
                var delta=Flat(Points[i+1]-Points[i]);var offset=Flat(point-Points[i]);
                float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                float sq=(offset-delta*t).sqrMagnitude;
                if(sq<best){best=sq;fraction=t;nearest=i;}
            }
            segment=nearest;var tangent=Flat(Points[segment+1]-Points[segment]).normalized;
            var closest=Vector3.Lerp(Points[segment],Points[segment+1],fraction);
            lateral=Vector3.Dot(Flat(point-closest),Vector3.Cross(Vector3.up,tangent));
            return Mathf.Lerp(Meters[segment],Meters[segment+1],fraction);
        }
        public Vector3 At(float meters,out Vector3 tangent)
        {
            meters=Mathf.Clamp(meters,0,Length);int low=0,high=Meters.Count-1;
            while(high-low>1){int mid=(low+high)/2;if(Meters[mid]<meters)low=mid;else high=mid;}
            tangent=Flat(Points[high]-Points[low]).normalized;
            return Vector3.Lerp(Points[low],Points[high],Mathf.InverseLerp(Meters[low],Meters[high],meters));
        }
    }
}
