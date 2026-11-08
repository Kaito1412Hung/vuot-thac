using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    public static class FallBuoyancyValidation
    {
        class Drive : IBoatInput {public float Steer{get;set;} public float Throttle=>1;public bool ConsumeReset()=>false;}
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run this isolated physics check in Play Mode");
            var track=Object.FindAnyObjectByType<RiverTrack>();
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var scene=SceneManager.CreateScene("FallBuoyancyValidation",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var physics=scene.GetPhysicsScene();var report=new StringBuilder();int passed=0,total=0;
            try
            {
                foreach(var def in catalog.boats)foreach(var fall in track.falls)
                {
                    var sample=track.EvaluateAtDistance(fall.distance-fall.length*.5f-8);
                    var go=Object.Instantiate(def.prefab);SceneManager.MoveGameObjectToScene(go,scene);
                    try
                    {
                        foreach(var r in go.GetComponentsInChildren<Renderer>())r.enabled=false;
                        var body=go.GetComponent<BoatController>();body.Track=track;var drive=new Drive();body.Input=drive;
                        var rb=go.GetComponent<Rigidbody>();rb.interpolation=RigidbodyInterpolation.None;body.Place(track.AfterRaceInsertion(sample.center)+Vector3.up*body.FloatHeight,Quaternion.LookRotation(sample.tangent));rb.linearVelocity=sample.tangent*8;
                        float maxDepth=-100,maxPitch=0;bool crossed=false;int respawns=0;body.Respawned+=()=>respawns++;
                        for(int step=0;step<400;step++)
                        {
                            int hint=-1;var water=track.SampleWater(go.transform.position,ref hint);
                            var forward=go.transform.forward;forward.y=0;
                            drive.Steer=Mathf.Clamp(Vector3.SignedAngle(forward,water.tangent,Vector3.up)*.04f,-1,1);
                            body.SendMessage("FixedUpdate");physics.Simulate(.02f);
                            water=track.SampleWater(go.transform.position,ref hint);
                            maxDepth=Mathf.Max(maxDepth,water.height-go.transform.position.y);
                            maxPitch=Mathf.Max(maxPitch,Mathf.Abs(Mathf.Asin(Mathf.Clamp(go.transform.forward.y,-1,1))*Mathf.Rad2Deg));
                            if(water.distanceAlong>fall.distance+fall.length*.5f+10){crossed=true;break;}
                        }
                        total++;if(crossed&&respawns==0&&go.transform.up.y>.6f)passed++;
                        report.AppendLine(def.name+" fall="+fall.distance+" crossed="+crossed+" respawns="+respawns+" maxRootDepth="+maxDepth.ToString("F3")+" pitch="+maxPitch.ToString("F1"));
                    }
                    finally{Object.DestroyImmediate(go);}
                }
            }
            finally{SceneManager.UnloadSceneAsync(scene);Shader.SetGlobalInt("_BoatDrySliceCount",0);}
            report.Insert(0,"Passed crossings="+passed+"/"+total+" (isolated physics, entry speed 8m/s, fixed step 0.02s)\n");
            Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/fall-buoyancy.txt",report.ToString());return report.ToString();
        }
    }
}
