using UnityEngine;
using System.Linq;
using UnityEditor;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    public static class CaveDoorClearance
    {
        public static int Clear(RiverTrack track)
        {
            int removed=0;
            foreach(Transform cave in track.transform.Find("Generated/BranchCaves").Cast<Transform>().Concat(track.transform.Find("Generated/Tunnels").Cast<Transform>()))
            {
                var frames=CaveInteriorThemes.Frames(track,cave);if(frames.Count<4)continue;
                foreach(int side in new[]{-1,1})
                {
                    var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];
                    var start=f.p+f.right*side*(f.half*.60f)+Vector3.up;var end=f.p+f.right*side*(f.half*.72f+8)+Vector3.up;
                    var axis=end-start;axis.y=0;
                    foreach(var renderer in cave.Find("MajesticChambers").GetComponentsInChildren<MeshRenderer>())
                    {
                        if(renderer.name.StartsWith("SculptedStoneCascade"))
                        {
                            var filter=renderer.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var vertices=mesh.vertices;var tri=mesh.GetTriangles(0);var keep=new System.Collections.Generic.List<int>();bool changed=false;
                            for(int i=0;i<tri.Length;i+=3){var midPoint=filter.transform.TransformPoint((vertices[tri[i]]+vertices[tri[i+1]]+vertices[tri[i+2]])/3);var d=midPoint-start;float along=Vector3.Dot(d,axis.normalized),x=Vector3.Dot(d,f.forward),y=d.y+1.2f;bool inside=along>-.6f&&along<axis.magnitude+.6f&&x*x/(3.4f*3.4f)+y*y/(9.4f*9.4f)<1;if(inside){changed=true;continue;}keep.Add(tri[i]);keep.Add(tri[i+1]);keep.Add(tri[i+2]);}
                            if(changed){mesh.SetTriangles(keep,0);mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);}continue;
                        }
                        string n=renderer.name;if(!(n.Contains("Column")||n.Contains("Stalagmite")||n.Contains("Dome")||n=="RoyalOrganFlowstone"))continue;
                        var p=renderer.transform.position;if(p.y>start.y+2||renderer.bounds.max.y<start.y+2)continue;
                        float u=Mathf.Clamp01(Vector3.Dot(p-start,axis)/axis.sqrMagnitude);var closest=start+axis*u;
                        float radius=Mathf.Min(renderer.bounds.extents.x,renderer.bounds.extents.z);
                        if(new Vector2(p.x-closest.x,p.z-closest.z).magnitude<2.8f+radius)
                        {renderer.gameObject.SetActive(false);removed++;}
                    }
                }
            }
            return removed;
        }
    }
}


