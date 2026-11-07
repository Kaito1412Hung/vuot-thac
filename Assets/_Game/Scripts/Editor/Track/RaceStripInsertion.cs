using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
using VuotThac.World;

namespace VuotThac.EditorTools.Track
{
    public static class RaceStripInsertion
    {
        public static string LastReport { get; private set; }
        public static void Apply(RiverTrack track,float width,List<Mesh> generatedMeshes=null)
        {
            if(track.appliedRaceStripWidth>0)throw new InvalidOperationException("Insertion already applied. Rebuild the map to change its width.");
            var root=track.transform.Find("Generated");
            float half=width*0.5f;
            Vector3 Shift(Vector3 p)
            {
                float delta=p.x-track.CenterXAtMapZ(p.z);
                return Mathf.Abs(delta)<0.001f?Vector3.zero:new Vector3(Mathf.Sign(delta)*half,0,0);
            }
            string assetPath=AssetDatabase.GetAssetPath(root.Find("Water/Water").GetComponent<MeshFilter>().sharedMesh);
            int moved=0,landTriangles=0;double oldArea=0,newArea=0;
            void DeformMesh(MeshFilter filter,bool land,bool clone)
            {
                var source=filter.sharedMesh;
                var mesh=clone?UnityEngine.Object.Instantiate(source):source;
                if(clone)
                {
                    mesh.name=source.name+"_RaceInsertion";filter.sharedMesh=mesh;
                    if(generatedMeshes!=null)generatedMeshes.Add(mesh);
                    else AssetDatabase.AddObjectToAsset(mesh,assetPath);
                }
                var vertices=mesh.vertices;
                var previous=(Vector3[])vertices.Clone();
                for(int i=0;i<vertices.Length;i++)
                {
                    var world=filter.transform.TransformPoint(vertices[i]);
                    vertices[i]=filter.transform.InverseTransformPoint(world+Shift(world));
                }
                if(land)
                {
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var indices=mesh.GetTriangles(sub);
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            int a=indices[i],b=indices[i+1],c=indices[i+2];
                            var pa=filter.transform.TransformPoint(previous[a]);var pb=filter.transform.TransformPoint(previous[b]);var pc=filter.transform.TransformPoint(previous[c]);
                            if(Shift(pa)!=Shift(pb)||Shift(pa)!=Shift(pc))continue;
                            landTriangles++;
                            oldArea+=Vector3.Cross(pb-pa,pc-pa).magnitude*0.5;
                            var na=filter.transform.TransformPoint(vertices[a]);var nb=filter.transform.TransformPoint(vertices[b]);var nc=filter.transform.TransformPoint(vertices[c]);
                            newArea+=Vector3.Cross(nb-na,nc-na).magnitude*0.5;
                        }
                    }
                }
                mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();
                var collider=filter.GetComponent<MeshCollider>();
                if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh;}
                EditorUtility.SetDirty(mesh);
            }
            void MoveRoot(Transform item)
            {
                Vector3 anchor=item.position;
                if(anchor.sqrMagnitude<0.0001f)
                {
                    var renderers=item.GetComponentsInChildren<Renderer>();
                    if(renderers.Length>0)
                    {
                        var bounds=renderers[0].bounds;
                        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                        anchor=bounds.center;
                    }
                }
                Vector3 delta=Shift(anchor);item.position+=delta;moved++;
                foreach(var farmer in item.GetComponentsInChildren<RealisticVillager>()){farmer.routeA+=delta;farmer.routeB+=delta;EditorUtility.SetDirty(farmer);}
                foreach(var farmer in item.GetComponentsInChildren<RuralVillager>()){farmer.routeA+=delta;farmer.routeB+=delta;EditorUtility.SetDirty(farmer);}
                foreach(var line in item.GetComponentsInChildren<LineRenderer>())
                    if(line.useWorldSpace){var points=new Vector3[line.positionCount];line.GetPositions(points);for(int i=0;i<points.Length;i++)points[i]+=delta;line.SetPositions(points);}
            }
            foreach(Transform group in root)
            {
                if(group.name=="Bridge")
                {
                    foreach(var filter in group.GetComponentsInChildren<MeshFilter>())RebuildBridge(track,filter,width);
                }
                else if(group.name=="Terrain"||group.name=="Mountains"||group.name=="Water")
                {
                    foreach(var filter in group.GetComponentsInChildren<MeshFilter>())DeformMesh(filter,group.name=="Terrain"||group.name=="Mountains",false);
                }
                else if(group.name=="Gates")
                {
                    foreach(Transform gate in group)foreach(Transform part in gate)
                        if(part.name=="Banner")DeformMesh(part.GetComponent<MeshFilter>(),false,true);else MoveRoot(part);
                }
                else if(group.name=="Villages"||group.name=="Animals")
                {
                    foreach(Transform subgroup in group)foreach(Transform item in subgroup)MoveRoot(item);
                }
                else foreach(Transform item in group)
                {
                    if(group.name=="MeadowGrass"&&item.name.StartsWith("MeadowGrass_"))DeformMesh(item.GetComponent<MeshFilter>(),false,false);
                    else MoveRoot(item);
                }
            }
            track.insertedRaceStripWidth=width;track.appliedRaceStripWidth=width;
            AlignCreekMouths(track);
            EditorUtility.SetDirty(track);Physics.SyncTransforms();
            LastReport=$"Inserted {width:0.0}m; translated {moved} object roots. Rigid terrain triangles {landTriangles}; original area={oldArea:F4}, new area={newArea:F4}, difference={newArea-oldArea:F6} m2.";
        }

        public static void RebuildBridge(RiverTrack track,MeshFilter filter,float insertedWidth)
        {
            var sample=track.GetNearest(filter.transform.position);
            float originalSpan=2*(track.HalfWidth+track.bankWidth+3);
            Vector3 crossing=sample.right*originalSpan+Vector3.right*insertedWidth;
            // Preserve the original arch proportions across the complete new span.
            float rise=3.5f*crossing.magnitude/originalSpan;
            var rebuilt=PropLibrary.BuildBridge(filter.sharedMesh.name,crossing.magnitude,rise);
            // Replace geometry in-place so the scene keeps its existing asset reference.
            EditorUtility.CopySerialized(rebuilt,filter.sharedMesh);
            UnityEngine.Object.DestroyImmediate(rebuilt);
            // CopySerialized chỉ đổi dữ liệu phía CPU, card đồ họa vẫn vẽ hình cũ. Gán lại để Unity tải mesh mới lên.
            var mesh=filter.sharedMesh;
            mesh.SetVertices(mesh.vertices);
            for(int sub=0;sub<mesh.subMeshCount;sub++)mesh.SetTriangles(mesh.GetTriangles(sub),sub,true);
            mesh.UploadMeshData(false);
            filter.transform.rotation=Quaternion.LookRotation(Vector3.Cross(crossing.normalized,Vector3.up),Vector3.up);
            var collider=filter.GetComponent<MeshCollider>();
            if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=null;collider.sharedMesh=filter.sharedMesh;
            EditorUtility.SetDirty(filter.sharedMesh);
        }

        public static int AlignCreekMouths(RiverTrack track)
        {
            var root=track.transform.Find("Generated");
            var probes=new List<MeshCollider>();
            foreach(var water in root.Find("Water").GetComponentsInChildren<MeshFilter>())
            { var probe=water.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=water.sharedMesh;probes.Add(probe); }
            int aligned=0;
            try
            {
                Physics.SyncTransforms();
                foreach(Transform waterfall in root.Find("MountainWaterfall"))
                {
                    var filter=waterfall.Find("CreekToMainRiver").GetComponent<MeshFilter>();
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                    const int columns=17;int rows=vertices.Length/columns;
                    for(int col=0;col<columns;col++)
                    {
                        var end=filter.transform.TransformPoint(vertices[(rows-1)*columns+col]);
                        float side=Mathf.Sign(end.x-track.CenterXAtMapZ(end.z));
                        var sample=end-Vector3.right*side*0.5f;
                        RaycastHit hit=new RaycastHit();bool found=false;
                        foreach(var probe in probes)if(probe.Raycast(new Ray(sample+Vector3.up*100,Vector3.down),out hit,200)){found=true;break;}
                        if(!found)continue;
                        float delta=hit.point.y-end.y;
                        for(int row=rows-17;row<rows;row++)
                        {
                            int index=row*columns+col;
                            var p=filter.transform.TransformPoint(vertices[index]);
                            p.y+=delta*Mathf.SmoothStep(0,1,(row-(rows-17))/16f);
                            vertices[index]=filter.transform.InverseTransformPoint(p);
                        }
                        aligned++;
                    }
                    mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);
                }
            }
            finally{foreach(var probe in probes)UnityEngine.Object.DestroyImmediate(probe);}
            return aligned;
        }
    }
}

