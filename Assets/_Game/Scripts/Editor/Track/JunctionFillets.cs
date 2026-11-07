using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class JunctionFillets
    {
        public static int Build(RiverTrack track)
        {
            var root=track.transform.Find("Generated/Water");var old=root.Find("RoundedShoreWater");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var main=root.Find("Water").GetComponent<MeshFilter>();var original=main.sharedMesh.vertices;
            var vertices=new List<Vector3>();var triangles=new List<int>();int corners=0;
            foreach(var branch in track.Branches)
            {
                var bank=new List<Vector3>();for(int i=0;i<original.Length/5;i++)bank.Add(main.transform.TransformPoint(original[i*5+(branch.side<0?0:4)]));bank.Sort((a,b)=>a.z.CompareTo(b.z));
                Vector3 Edge(float z){int lo=0,hi=bank.Count-1;while(hi-lo>1){int mid=(lo+hi)/2;if(bank[mid].z<z)lo=mid;else hi=mid;}return Vector3.Lerp(bank[lo],bank[hi],Mathf.InverseLerp(bank[lo].z,bank[hi].z,z));}
                Vector2 Dist(Vector3 p){float mainD=branch.side*(p.x-Edge(p.z).x);var map=p-Vector3.right*branch.side*track.appliedRaceStripWidth*.5f;float branchD=branch.GetNearest(map).horizontalDistance-branch.HalfWidth-track.waterSurfaceExtraWidth;var start=branch.EvaluateAtDistance(0);var end=branch.EvaluateAtDistance(branch.Length);branchD=Mathf.Max(branchD,Mathf.Max(-Vector3.Dot(map-start.center,start.tangent),Vector3.Dot(map-end.center,end.tangent)));return new Vector2(mainD,branchD);}
                float Field(Vector3 p,int field){var d=Dist(p);if(field==0)return d.x;if(field==1)return d.y;float h=Mathf.Max(18-Mathf.Abs(d.x-d.y),0)/18;return -(Mathf.Min(d.x,d.y)-h*h*4.5f);}
                void Add(Vector3 a,Vector3 b,Vector3 c)
                {
                    var poly=new List<Vector3>{a,b,c};
                    for(int field=0;field<3;field++)
                    {
                        var next=new List<Vector3>();for(int i=0;i<poly.Count;i++){var p=poly[i];var q=poly[(i+1)%poly.Count];float dp=Field(p,field),dq=Field(q,field);if(dp>=0)next.Add(p);if((dp>=0)!=(dq>=0)){float lo=0,hi=1;for(int j=0;j<16;j++){float mid=(lo+hi)*.5f;if((Field(Vector3.Lerp(p,q,mid),field)>=0)==(dp>=0))lo=mid;else hi=mid;}next.Add(Vector3.Lerp(p,q,(lo+hi)*.5f));}}poly=next;
                    }
                    for(int i=1;i<poly.Count-1;i++){var p=poly[0];var q=poly[i];var r=poly[i+1];if(Vector3.Cross(q-p,r-p).sqrMagnitude<1e-9f)continue;foreach(var v in new[]{p,q,r}){var w=v;w.y=Edge(w.z).y;triangles.Add(vertices.Count);vertices.Add(w);}}
                }
                foreach(float d in new[]{0f,branch.Length})
                {
                    var center=branch.EvaluateAtDistance(d).center+Vector3.right*branch.side*track.appliedRaceStripWidth*.5f;
                    for(float x=center.x-23;x<center.x+23;x+=.65f)for(float z=center.z-23;z<center.z+23;z+=.65f)
                    {var a=new Vector3(x,0,z);var b=a+Vector3.forward*.65f;var c=b+Vector3.right*.65f;var e=a+Vector3.right*.65f;Add(a,b,c);Add(a,c,e);}corners+=2;
                }
            }
            string path="Assets/_Game/Generated/RoundedShoreWater.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh{name="RoundedShoreWater"};AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var obj=new GameObject("RoundedShoreWater");obj.transform.SetParent(root,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=main.GetComponent<Renderer>().sharedMaterial;EditorUtility.SetDirty(mesh);return triangles.Count/3;
        }
    }
}



