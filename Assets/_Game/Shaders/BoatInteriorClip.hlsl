#ifndef VUOT_THAC_BOAT_INTERIOR_CLIP
#define VUOT_THAC_BOAT_INTERIOR_CLIP
int _BoatDrySliceCount;
float4x4 _BoatDryWorldToLocal;
float4 _BoatDryVolume;
float4 _BoatDrySlices[96];
void ClipWaterInsideBoat(float3 positionWS)
{
    if(_BoatDrySliceCount<2)return;
    float3 p=mul(_BoatDryWorldToLocal,float4(positionWS,1)).xyz;
    if(p.z<_BoatDryVolume.x||p.z>_BoatDryVolume.y||p.y<_BoatDryVolume.z||p.y>_BoatDryVolume.w)return;
    int low=0,high=_BoatDrySliceCount-1;
    [loop] while(high-low>1){int mid=(low+high)/2;if(p.z<_BoatDrySlices[mid].z)high=mid;else low=mid;}
    float4 a=_BoatDrySlices[low],b=_BoatDrySlices[high];
    float t=saturate((p.z-a.z)/max(.0001,b.z-a.z));
    float2 sides=lerp(a.xy,b.xy,t);
    if(p.x>sides.x&&p.x<sides.y)clip(-1);
}
#endif
