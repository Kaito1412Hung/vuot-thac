#ifndef VUOT_THAC_CAVE_EXTERIOR_CLIP
#define VUOT_THAC_CAVE_EXTERIOR_CLIP
float _CaveInteriorMode;
float _CaveOwner;
int _CaveInteriorCount;
float4 _CaveStableCenters[256];
float4 _CaveStableAxes[256];
// Camera đang ở trong hang nào (0 là ở ngoài). CaveInteriorVisibility tính một lần cho mỗi camera trên CPU,
// thay vì mỗi đỉnh lưới phải lặp qua cả trăm vùng hang như trước (rất nặng vì map có hàng triệu đỉnh).
float _CaveCameraOwner;
int _CaveRoomCount;
float4 _CaveRoomCenters[96];
float4 _CaveRoomAxes[96];
float CaveCameraInside(float3 cameraPosition)
{
    return _CaveCameraOwner;
}
void ClipExteriorInsideCave(float3 positionWS,float cameraInside)
{
    if(_CaveInteriorMode>0.5){if(cameraInside>.5&&abs(cameraInside-_CaveOwner)>.1)clip(-1);return;}
    if(cameraInside<0.5)return;
    // Chỉ xét các vùng của đúng hang camera đang ở (C# lọc sẵn), không lặp qua cả trăm vùng cho mỗi điểm ảnh
    [loop] for(int i=0;i<_CaveRoomCount;i++)
    {
        float4 center=_CaveRoomCenters[i],axis=_CaveRoomAxes[i];
        float owner=floor(axis.w/100),halfLength=axis.w-owner*100;
        float3 delta=positionWS-center.xyz;
        float along=-delta.x*axis.y+delta.z*axis.x;
        if(delta.y<-.35||delta.y>axis.z+1||abs(along)>halfLength)continue;
        float across=dot(delta.xz,axis.xy)/center.w;
        float vertical=(delta.y+1.8)/(axis.z+1.8);
        if(across*across+vertical*vertical<1.25)clip(-1);
    }
}
#endif



