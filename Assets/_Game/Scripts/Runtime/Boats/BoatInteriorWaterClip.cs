using System.Collections.Generic;
using UnityEngine;

namespace VuotThac.Boats
{
    // Remove only water fragments inside the actual open-hull footprint, including sloping fall water.
    [DefaultExecutionOrder(150)]
    public class BoatInteriorWaterClip : MonoBehaviour
    {
        static readonly List<BoatInteriorWaterClip> instances=new List<BoatInteriorWaterClip>();
        static BoatInteriorWaterClip publisher;
        static readonly int CountId=Shader.PropertyToID("_BoatDrySliceCount");
        static readonly int MatrixId=Shader.PropertyToID("_BoatDryWorldToLocal");
        static readonly int SlicesId=Shader.PropertyToID("_BoatDrySlices");
        static readonly int VolumeId=Shader.PropertyToID("_BoatDryVolume");
        readonly Vector4[] slices=new Vector4[96];
        BoatController boat;
        int count;
        Vector4 volume;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){instances.Clear();publisher=null;Shader.SetGlobalInt(CountId,0);}
        void OnEnable(){boat=GetComponent<BoatController>();if(!instances.Contains(this))instances.Add(this);Capture();}
        void Capture()
        {
            count=0;if(boat==null||boat.model==null)return;
            var mask=boat.model.Find("DryInteriorWaterMask");if(mask==null)return;
            var mesh=mask.GetComponent<MeshFilter>().sharedMesh;if(mesh==null)return;
            var rows=new SortedDictionary<float,Vector4>();
            var toModel=Matrix4x4.TRS(mask.localPosition,mask.localRotation,mask.localScale);
            foreach(var v in mesh.vertices)
            {
                var p=toModel.MultiplyPoint3x4(v);
                float z=Mathf.Round(p.z*1000)*.001f;
                if(rows.TryGetValue(z,out var row)){row.x=Mathf.Min(row.x,p.x);row.y=Mathf.Max(row.y,p.x);row.w=Mathf.Max(row.w,p.y);rows[z]=row;}
                else rows[z]=new Vector4(p.x,p.x,z,p.y);
            }
            if(rows.Count>96){Debug.LogError("Boat water footprint exceeds shader capacity",this);return;}
            float high=-1000;
            foreach(var row in rows.Values){slices[count++]=row;high=Mathf.Max(high,row.w);}
            if(count>1)volume=new Vector4(slices[0].z,slices[count-1].z,-boat.hullDepth-.08f,high+1.5f);
        }
        void LateUpdate()
        {
            BoatInteriorWaterClip selected=null;int score=-1;
            foreach(var candidate in instances)
            {
                if(candidate==null||!candidate.isActiveAndEnabled||candidate.boat==null)continue;
                int value=candidate.boat.Input is PlayerBoatInput?3:candidate.name.StartsWith("Preview_")?2:1;
                if(value>score){score=value;selected=candidate;}
            }
            if(selected!=this)return;
            publisher=this;
            if(count==0)Capture();
            if(count<2||boat.transform.up.y<.35f){Shader.SetGlobalInt(CountId,0);return;}
            Shader.SetGlobalMatrix(MatrixId,boat.model.worldToLocalMatrix);
            Shader.SetGlobalVector(VolumeId,volume);
            Shader.SetGlobalVectorArray(SlicesId,slices);
            Shader.SetGlobalInt(CountId,count);
        }
        void OnDisable(){instances.Remove(this);if(publisher==this){publisher=null;Shader.SetGlobalInt(CountId,0);}}
    }
}
