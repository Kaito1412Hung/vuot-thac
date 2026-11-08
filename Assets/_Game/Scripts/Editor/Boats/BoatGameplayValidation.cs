using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    // Uses the real scene's colliders; run in Play Mode so collision callbacks are included.
    public static class BoatGameplayValidation
    {
        class Drive : IBoatInput
        {
            public float Steer { get; set; }
            public float Throttle { get; set; }
            public bool ConsumeReset()=>false;
        }
        public static string Run(string suite)
        {
            if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run in Play Mode");
            var track=Object.FindAnyObjectByType<RiverTrack>();
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var report=new StringBuilder();int passed=0,total=0;
            try
            {
                foreach(var def in catalog.boats)
                {
                    float[] distances=suite=="shore"?new[]{230f,370f,630f}:suite=="falls"?new[]{150f,575f,760f}:new[]{440f};
                    foreach(float distance in distances)for(int side=-1;side<=1;side+=2)
                    {
                        if(suite!="shore"&&side==1)continue;
                        var go=Object.Instantiate(def.prefab);
                        try
                        {
                            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.enabled=false;
                            var body=go.GetComponent<BoatController>();body.Track=track;
                            var drive=new Drive();body.Input=drive;
                            var rb=go.GetComponent<Rigidbody>();rb.interpolation=RigidbodyInterpolation.None;
                            var sample=track.EvaluateAtDistance(suite=="falls"?distance-13:distance);
                            var center=track.AfterRaceInsertion(sample.center);Vector3 spawn=center,heading=sample.tangent;
                            if(suite=="shore")
                            {
                                int edgeHint=-1;
                                for(float x=0;x<60;x+=.25f)
                                {
                                    var p=center+sample.right*x*side;
                                    if(!track.SampleWater(p,ref edgeHint).inWater){spawn=p-sample.right*side*.95f;break;}
                                }
                                heading=Quaternion.AngleAxis(side*30,Vector3.up)*heading;
                            }
                            spawn.y=sample.WaterHeight+body.FloatHeight-(suite=="immersion"?.6f:0);
                            body.Place(spawn,Quaternion.LookRotation(heading));Physics.SyncTransforms();
                            rb.linearVelocity=suite=="immersion"?Vector3.down*6:heading*(suite=="falls"?10:6);
                            int resets=0;string reason="";body.Respawned+=()=>{resets++;reason=body.LastRecoveryReason;};
                            float minClear=100,minUp=1,maxRise=-100;int hint=-1;
                            for(int step=0;step<(suite=="shore"?500:400);step++)
                            {
                                var w=track.SampleWater(go.transform.position,ref hint);
                                var target=w.center+w.tangent*8-go.transform.position;target.y=0;
                                drive.Throttle=1;drive.Steer=Mathf.Clamp(Vector3.SignedAngle(go.transform.forward,target,Vector3.up)*.035f,-1,1);
                                if(suite=="shore"&&step<125){drive.Throttle=step<25?1:-1;drive.Steer=0;}
                                if(suite=="immersion"){drive.Throttle=0;drive.Steer=0;}
                                body.SendMessage("FixedUpdate");Physics.Simulate(.02f);
                                w=track.SampleWater(go.transform.position,ref hint);
                                minClear=Mathf.Min(minClear,go.transform.position.y-w.height);
                                minUp=Mathf.Min(minUp,go.transform.up.y);
                                maxRise=Mathf.Max(maxRise,go.transform.position.y-w.height-body.FloatHeight);
                                if(suite=="falls"&&w.distanceAlong>distance+18)break;
                            }
                            var end=track.SampleWater(go.transform.position,ref hint);
                            bool success=resets==0&&minUp>.6f;
                            float travelled=Vector3.ProjectOnPlane(go.transform.position-spawn,Vector3.up).magnitude;
                            if(suite=="shore")success&=(end.edgeDistance<-3||(body.Speed>3&&travelled>8))&&minClear>-.35f;
                            if(suite=="falls")success&=end.distanceAlong>distance+18&&minClear>-.35f;
                            if(suite=="immersion")success&=go.transform.position.y>end.height+body.FloatHeight-.08f&&maxRise<.4f;
                            total++;if(success)passed++;
                            report.AppendLine(def.name+" d="+distance+" side="+side+" pass="+success+" edge="+end.edgeDistance.ToString("F2")+" speed="+body.Speed.ToString("F1")+" travelled="+travelled.ToString("F1")+" minClear="+minClear.ToString("F3")+" minUp="+minUp.ToString("F2")+" maxRise="+maxRise.ToString("F2")+" respawns="+resets+" reason="+reason);
                        }
                        finally{Object.DestroyImmediate(go);}
                    }
                }
            }
            finally{Physics.simulationMode=previous;}
            report.Insert(0,suite+" real-map physics passed="+passed+"/"+total+"\n");
            Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/gameplay-"+suite+".txt",report.ToString());
            return report.ToString();
        }
    }
}
