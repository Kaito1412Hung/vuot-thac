using UnityEngine;

namespace VuotThac.Track
{
    // Shared by valley and mountain mesh builders: the waterfall is cut into
    // the mountain itself, and the receiving creek has a continuous riverbed.
    public class MountainWaterfallLandform : MonoBehaviour
    {
        public Vector3 origin, outward = Vector3.right;
        public float sourceHeight, lipHeight, poolHeight, riverHeight;
        public float riverRun = 85, sourceDistance = 60, crestDistance = 32;
        public bool configured;
        public int variant;
        public float creekBend = 2.5f, poolRadius = 13.5f;
        public Vector3 Right => Vector3.Cross(Vector3.up, outward).normalized;

        public Vector3 Local(Vector3 world)
        {
            var d = world - origin;
            return new Vector3(Vector3.Dot(d, Right), world.y, Vector3.Dot(d, outward));
        }
        public Vector3 World(float x, float y, float z) => origin + Right*x + Vector3.up*y + outward*z;
        public float CreekCenter(float z)
        {
            if(z>crestDistance)
            {
                float headT=Mathf.InverseLerp(crestDistance,sourceDistance,z);
                return Mathf.Sin(headT*Mathf.PI)*2.7f*(variant%2==0?1:-1);
            }
            if(variant == 0)
            {
                if(z>=0)return 0;
                float original=Mathf.Clamp01(-z/riverRun);
                return Mathf.Sin(original*Mathf.PI*2)*Mathf.Sin(original*Mathf.PI)*2.5f;
            }
            if(z>=-18)return 0;
            float t=Mathf.Clamp01((-z-18)/(riverRun-18));
            return Mathf.Sin(t*Mathf.PI*2)*Mathf.Sin(t*Mathf.PI)*creekBend;
        }
        public float WaterLevel(float z)
        {
            if(z>=crestDistance)return sourceHeight+Mathf.InverseLerp(crestDistance,sourceDistance,z)*0.65f;
            if (z >= 11) return Mathf.Lerp(lipHeight, sourceHeight, Mathf.InverseLerp(11, crestDistance, z));
            if (z > 3) return Mathf.Lerp(poolHeight, lipHeight, Mathf.SmoothStep(0,1,(z-3)/8));
            if (z >= -4) return poolHeight;
            // Ease into a level mouth before joining the main river.
            float t = Mathf.Clamp01((-z-4)/(riverRun-16));
            return Mathf.Lerp(poolHeight, riverHeight, Mathf.SmoothStep(0,1,t));
        }
        public float WaterWidth(float z)
        {
            if(z>crestDistance)
            {
                float dz=(z-crestDistance-13)*0.6f;
                return Mathf.Max(3.2f,2*Mathf.Sqrt(Mathf.Max(0,7.8f*7.8f-dz*dz)));
            }
            if(z<=3 && z>=-5-poolRadius)
            {
                float dz=z+5;
                return Mathf.Max(4.8f,2*Mathf.Sqrt(Mathf.Max(0,poolRadius*poolRadius-dz*dz)));
            }
            if (z > 11) return Mathf.Lerp(5.0f, 3.2f, Mathf.InverseLerp(11, crestDistance, z));
            if (z > 3) return Mathf.Lerp(7.0f, 5.0f, (z-3)/8);
            if (z > -5) return 8f;
            return Mathf.Lerp(4.8f, 3.2f, Mathf.Clamp01((-z-5)/(riverRun-5)));
        }
        public float ShapeSurface(Vector3 world, float naturalHeight)
        {
            if (!configured) return naturalHeight;
            var p = Local(world);
            if (p.z < -riverRun-3 || p.z > sourceDistance+5) return naturalHeight;
            float x = Mathf.Abs(p.x-CreekCenter(p.z));
            float inner = WaterWidth(p.z)*0.5f + (p.z > crestDistance-4 ? 3.5f : p.z > 3 ? 1.6f : 4.5f);
            float margin = p.z > 0 ? 18 : 10;
            float blend = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(inner,inner+margin,x));
            blend *= 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(sourceDistance-1,sourceDistance+5,p.z));
            blend *= Mathf.SmoothStep(0,1,Mathf.InverseLerp(-riverRun-3,-riverRun+1,p.z));
            float bed = WaterLevel(p.z) - (p.z > crestDistance-4 ? 1.0f : p.z > 3 ? 0.42f : 0.65f);
            // Banks rise back to the natural mountain instead of becoming a freestanding wall.
            return Mathf.Lerp(naturalHeight, bed, blend);
        }
    }
}
