using UnityEngine;

namespace VuotThac.Track
{
    public class ForestCaveLandform : MonoBehaviour
    {
        public Vector3 origin,forward;
        public bool configured;
        public int variant;
        public float bend=4.5f;
        public float ShapeSurface(Vector3 world,float natural)
        {
            if(!configured)return natural;
            var delta=world-origin;
            float z=Vector3.Dot(delta,forward);
            float x=Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward)));
            if(z< -17||z>1||x>15)return natural;
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-17,-4,z));
            blend*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,15,x));
            return Mathf.Lerp(natural,origin.y,blend);
        }
    }
}
