using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    public static class WaterJunctionRepair
    {
        public static string LastReport { get; private set; }
        static Vector2 UV(Vector3 p)=>new Vector2(p.x,p.z)*0.035f;
        sealed class Bank
        {
            public readonly List<Vector3> points=new();
            public Vector3 At(float z)
            {
                if(z<=points[0].z)return points[0];
                if(z>=points[points.Count-1].z)return points[points.Count-1];
                int lo=0,hi=points.Count-1;
                while(hi-lo>1){int mid=(lo+hi)/2;if(points[mid].z<z)lo=mid;else hi=mid;}
                return Vector3.Lerp(points[lo],points[hi],Mathf.InverseLerp(points[lo].z,points[hi].z,z));
            }
        }
        public static void Repair(RiverTrack track)
        {
            var root=track.transform.Find("Generated/Water");if(root==null)return;
            var main=root.Find("Water").GetComponent<MeshFilter>();
            var mv=main.sharedMesh.vertices;var left=new Bank();var right=new Bank();
            for(int row=0;row<mv.Length/5;row++)
            {
                left.points.Add(main.transform.TransformPoint(mv[row*5]));
                right.points.Add(main.transform.TransformPoint(mv[row*5+4]));
            }
            left.points.Sort((a,b)=>a.z.CompareTo(b.z));right.points.Sort((a,b)=>a.z.CompareTo(b.z));
            var mainUV=new List<Vector2>();var fall=new HashSet<int>();
            if(main.sharedMesh.subMeshCount>1)foreach(int i in main.sharedMesh.GetTriangles(1))fall.Add(i);
            var oldUV=main.sharedMesh.uv;
            for(int i=0;i<mv.Length;i++)mainUV.Add(fall.Contains(i)&&oldUV.Length==mv.Length?oldUV[i]:UV(main.transform.TransformPoint(mv[i])));
            main.sharedMesh.SetUVs(0,mainUV);main.sharedMesh.RecalculateTangents();EditorUtility.SetDirty(main.sharedMesh);
            int repaired=0,triangles=0;
            for(int bi=0;bi<track.Branches.Count;bi++)
            {
                var branch=track.Branches[bi];var child=root.Find("BranchWater_"+bi);
                if(branch==null||!branch.IsBuilt||child==null)continue;
                var filter=child.GetComponent<MeshFilter>();var bank=branch.side<0?left:right;
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var normals=new List<Vector3>();
                float Distance(Vector3 p)=>branch.side*(p.x-bank.At(p.z).x);
                Vector3 Intersection(Vector3 a,Vector3 b)
                {
                    float lo=0,hi=1;bool outside=Distance(a)>=0;
                    for(int step=0;step<26;step++){float mid=(lo+hi)*0.5f;bool side=Distance(Vector3.Lerp(a,b,mid))>=0;if(side==outside)lo=mid;else hi=mid;}
                    var p=Vector3.Lerp(a,b,(lo+hi)*0.5f);var edge=bank.At(p.z);
                    p.x=edge.x;p.y=edge.y;return p;
                }
                Vector3 Point(float distance,float across)
                {
                    var sample=branch.EvaluateAtDistance(distance);
                    var p=sample.center+sample.right*across;
                    p.x+=branch.side*track.appliedRaceStripWidth*0.5f;
                    var edge=bank.At(p.z);
                    float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(Distance(p)/12));
                    p.y=Mathf.Lerp(edge.y,p.y,blend);
                    return p;
                }
                void Triangle(Vector3 a,Vector3 b,Vector3 c)
                {
                    var source=new[]{a,b,c};var clipped=new List<Vector3>(4);
                    for(int j=0;j<3;j++)
                    {
                        var from=source[j];var to=source[(j+1)%3];bool keepFrom=Distance(from)>=0,keepTo=Distance(to)>=0;
                        if(keepFrom)clipped.Add(from);
                        if(keepFrom!=keepTo)clipped.Add(Intersection(from,to));
                    }
                    for(int j=1;j<clipped.Count-1;j++)
                    {
                        var p=clipped[0];var q=clipped[j];var r=clipped[j+1];
                        if(Vector3.Cross(q-p,r-p).sqrMagnitude<0.00000001f)continue;
                        if(Vector3.Cross(q-p,r-p).y<0){var temp=q;q=r;r=temp;}
                        foreach(var v in new[]{p,q,r}){indices.Add(vertices.Count);vertices.Add(filter.transform.InverseTransformPoint(v));uv.Add(UV(v));normals.Add(Vector3.up);}
                    }
                }
                const int columns=12;
                int rows=Mathf.CeilToInt(branch.Length/0.8f);
                float extent=branch.HalfWidth+track.waterSurfaceExtraWidth;
                for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
                {
                    float start=Mathf.Min(row*0.8f,branch.Length),end=Mathf.Min((row+1)*0.8f,branch.Length);
                    float x0=Mathf.Lerp(-extent,extent,col/(float)columns),x1=Mathf.Lerp(-extent,extent,(col+1)/(float)columns);
                    var a=Point(start,x0);var b=Point(end,x0);var c=Point(end,x1);var d=Point(start,x1);
                    Triangle(a,b,c);Triangle(a,c,d);
                }
                var mesh=filter.sharedMesh;mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
                var collider=filter.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh;}
                EditorUtility.SetDirty(mesh);repaired++;triangles+=indices.Count/3;
            }
            int mouths=RaceStripInsertion.AlignCreekMouths(track);
            LastReport=$"Aligned {mouths} creek mouth points. Rebuilt {repaired} branch surfaces / {repaired*2} main-river junctions, {triangles} triangles. Exact bank clipping and shared heights/UVs.";
        }
    }
}

