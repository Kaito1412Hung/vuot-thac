using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class ShorelineFinish
    {
        public static int Repair(RiverTrack track)
        {
            var mouths=new List<Vector3>();
            foreach(var b in track.Branches)foreach(float d in new[]{0f,b.Length})
            {var p=b.EvaluateAtDistance(d).center;p.x+=b.side*track.appliedRaceStripWidth*.5f;mouths.Add(p);}
            int count=0;
            var caps=new List<Vector3>();
            var waterRoot=track.transform.Find("Generated/Water");
            if(waterRoot.Find("ShoreCornerCaps")!=null)return 0;
            var probes=new List<MeshCollider>();
            foreach(var f in track.transform.Find("Generated/Water").GetComponentsInChildren<MeshFilter>())
            {var probe=f.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=f.sharedMesh;probes.Add(probe);}
            Physics.SyncTransforms();
            foreach(var filter in track.transform.Find("Generated/Terrain").GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var original=mesh.vertices;var oldUV=mesh.uv;
                var v=new List<Vector3>();var uv=new List<Vector2>();var subs=new List<int[]>();bool changed=false;
                bool Near(Vector3 p,float margin){foreach(var m in mouths)if(new Vector2(p.x-m.x,p.z-m.z).magnitude<22+margin)return true;return false;}
                Vector3 Adjust(Vector3 p)
                {
                    if(!Near(p,0))return p;
                    foreach(var probe in probes)
                    if(probe.Raycast(new Ray(new Vector3(p.x,150,p.z),Vector3.down),out var hit,300))
                    {if(p.y>hit.point.y-.2f&&p.y<hit.point.y+3){p.y=hit.point.y-.2f;changed=true;}break;}
                    return p;
                }
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var indices=new List<int>();
                    void Add(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc,int depth)
                    {
                        var mid=(a+b+c)/3;float size=Mathf.Max((a-b).magnitude,Mathf.Max((b-c).magnitude,(c-a).magnitude));
                        if(depth<5&&size>.65f&&Near(mid,size))
                        {var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;var uab=(ua+ub)*.5f;var ubc=(ub+uc)*.5f;var uca=(uc+ua)*.5f;
                         Add(a,ab,ca,ua,uab,uca,depth+1);Add(ab,b,bc,uab,ub,ubc,depth+1);Add(ca,bc,c,uca,ubc,uc,depth+1);Add(ab,bc,ca,uab,ubc,uca,depth+1);return;}
                        var aa=Adjust(a);var bb=Adjust(b);var cc=Adjust(c);
                        foreach(var p in new[]{aa,bb,cc}){indices.Add(v.Count);v.Add(filter.transform.InverseTransformPoint(p));}
                        uv.Add(ua);uv.Add(ub);uv.Add(uc);
                    }
                    var tri=mesh.GetTriangles(sub);
                    for(int j=0;j<tri.Length;j+=3){int a=tri[j],b=tri[j+1],c=tri[j+2];Add(filter.transform.TransformPoint(original[a]),filter.transform.TransformPoint(original[b]),filter.transform.TransformPoint(original[c]),oldUV[a],oldUV[b],oldUV[c],0);}
                    subs.Add(indices.ToArray());
                }
                if(!changed)continue;
                mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.subMeshCount=subs.Count;
                for(int i=0;i<subs.Count;i++)mesh.SetTriangles(subs[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var collider=filter.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh;}
                EditorUtility.SetDirty(mesh);count++;
            }
            foreach(var probe in probes)UnityEngine.Object.DestroyImmediate(probe);
            return count;
        }
    }
}



