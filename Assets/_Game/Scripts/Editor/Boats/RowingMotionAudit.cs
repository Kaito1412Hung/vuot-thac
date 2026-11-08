using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    public static class RowingMotionAudit
    {
        public static string Run()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var report=new StringBuilder();int poses=0,contacts=0;float worstGrip=0,loopJump=0;
            foreach(var definition in catalog.boats)
            {
                var root=Object.Instantiate(definition.prefab,new Vector3(0,500,0),Quaternion.identity);
                try
                {
                    root.GetComponent<Rigidbody>().isKinematic=true;
                    var actors=root.GetComponentsInChildren<BoatRower>();
                    var colliders=new List<MeshCollider>();
                    foreach(var mf in root.transform.Find("Model").GetComponentsInChildren<MeshFilter>())
                    {
                        if(mf.GetComponentInParent<BoatRower>()!=null||mf.name.Contains("Water")||mf.sharedMesh==null)continue;
                        var col=mf.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mf.sharedMesh;colliders.Add(col);
                    }
                    Physics.SyncTransforms();int hitCount=0;float grip=0,deepest=-100,returnDepth=-100;
                    foreach(var actor in actors)
                    {
                        actor.Capture();if(actor.stroke==BoatRower.Stroke.None)continue;
                        foreach(float effort in new[]{0f,.45f,1f})
                        for(int phase=0;phase<240;phase++)
                        {
                            actor.Pose(phase/120f,effort);poses++;grip=Mathf.Max(grip,actor.GripError);
                            var p=actor.paddle;var bounds=p.GetComponent<MeshFilter>().sharedMesh.bounds;
                            if(effort>.99f)
                            {
                                float low=p.TransformPoint(0,bounds.min.y,0).y;
                                if(actor.stroke==BoatRower.Stroke.Double)low=Mathf.Min(low,p.TransformPoint(0,bounds.max.y,0).y);
                                float depth=root.transform.position.y-root.GetComponent<BoatController>().FloatHeight-low;
                                deepest=Mathf.Max(deepest,depth);
                                if(actor.stroke!=BoatRower.Stroke.Double&&phase%120>=90&&phase%120<=110)returnDepth=Mathf.Max(returnDepth,depth);
                            }
                            bool hit=false;string what="";
                            foreach(var offset in new[]{Vector3.zero,Vector3.right*.022f,Vector3.left*.022f,Vector3.forward*.022f,Vector3.back*.022f})
                            {
                                var a=p.TransformPoint(new Vector3(0,bounds.max.y,0)+offset);
                                var b=p.TransformPoint(new Vector3(0,bounds.min.y,0)+offset);
                                if(Touches(colliders,a,b)){hit=true;what="paddle";break;}
                            }
                            foreach(bool left in new[]{true,false})
                            {
                                var shoulder=actor.rig.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                                var elbow=actor.rig.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                                var wrist=actor.rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                                if(Touches(colliders,shoulder.position,elbow.position)||Touches(colliders,elbow.position,wrist.position)){hit=true;what+=" arm";}
                            }
                            if(hit){hitCount++;if(hitCount<=4)report.AppendLine(definition.name+" phase="+phase+" effort="+effort+" contact="+what);}
                        }
                        foreach(float boundary in new[]{1f,2f})
                        {
                            actor.Pose(boundary-.00001f,1);var previous=actor.paddle.TransformPoint(0,-actor.ShaftLength*.5f,0);
                            actor.Pose(boundary+.00001f,1);loopJump=Mathf.Max(loopJump,Vector3.Distance(previous,actor.paddle.TransformPoint(0,-actor.ShaftLength*.5f,0)));
                        }
                    }
                    report.AppendLine(definition.name+" contacts="+hitCount+" maxGripError="+grip.ToString("F4")+" deepestBlade="+deepest.ToString("F3")+" maxReturnDepth="+returnDepth.ToString("F3"));contacts+=hitCount;worstGrip=Mathf.Max(worstGrip,grip);
                }
                finally{Object.DestroyImmediate(root);}
            }
            report.Insert(0,"Poses="+poses+" ContactPoses="+contacts+" MaxGripError="+worstGrip.ToString("F4")+" LoopJump="+loopJump.ToString("F5")+"\n");
            Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/rowing-motion.txt",report.ToString());return report.ToString();
        }
        static bool Touches(List<MeshCollider> hull,Vector3 a,Vector3 b)
        {
            var delta=b-a;if(delta.sqrMagnitude<1e-8f)return false;
            foreach(var c in hull)if(c.Raycast(new Ray(a,delta.normalized),out _,delta.magnitude)||c.Raycast(new Ray(b,-delta.normalized),out _,delta.magnitude))return true;
            return false;
        }
    }
}
