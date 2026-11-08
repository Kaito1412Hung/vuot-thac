using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    public static class BoatLearningValidation
    {
        [System.Serializable] public class ObservationAudit {public ObservationRow[] rows;}
        [System.Serializable] public class ObservationRow {public float[] observations,modelAction,expertAction;}
        // Identical map, hulls, lanes and routes for both controllers; traffic stays enabled.
        public static string Run(bool learned=true,int maxSteps=15000,int difficulty=2,int scenario=0)
        {
            if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run in Play Mode");
            var model=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/_Game/Resources/AI/SuoiRungBoat.onnx");
            if(learned&&model==null)throw new System.InvalidOperationException("Trained ONNX missing");
            var track=Object.FindAnyObjectByType<RiverTrack>();
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var existing=Object.FindObjectsByType<BoatController>(FindObjectsSortMode.None);
            foreach(var old in existing)old.gameObject.SetActive(false);
            var boats=new BoatController[6];var progress=new RaceProgress[6];
            int[] impacts=new int[6],rescues=new int[6];
            var previous=Physics.simulationMode;
            bool automatic=Academy.Instance.AutomaticSteppingEnabled;
            Physics.simulationMode=SimulationMode.Script;Academy.Instance.AutomaticSteppingEnabled=false;
            var report=new StringBuilder();
            try
            {
                var s=track.EvaluateAtDistance(track.StartDistance-12);
                float spacing=5;
                for(int i=0;i<6;i++)
                {
                    int index=i;var boat=Object.Instantiate(catalog.boats[(i*2)%catalog.boats.Length].prefab).GetComponent<BoatController>();
                    boats[i]=boat;boat.Track=track;
                    int lane=scenario==1?5-i:i;
                    var p=track.AfterRaceInsertion(s.center)+s.right*((lane-2.5f)*spacing)-s.tangent*boat.hullLength*.475f;
                    p.y=s.WaterHeight+boat.FloatHeight+.02f;boat.Place(p,Quaternion.LookRotation(s.tangent));
                    boat.enabled=false;boat.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;
                    if(boat.model!=null)boat.model.gameObject.SetActive(false);
                    progress[i]=boat.gameObject.AddComponent<RaceProgress>();progress[i].Initialize(boat,"Boat "+i);
                    int route=i<2?i*2:(i-2)*2+1;
                    if(learned)BoatLearningAgent.Attach(boat,route,false,model).SetDifficulty((DifficultyLevel)difficulty);
                    else{var ai=boat.gameObject.AddComponent<BoatAIInput>();ai.Initialize(boat,route);ai.ConfigureDifficulty((DifficultyLevel)difficulty);ai.enabled=false;}
                    boat.Impact+=speed=>impacts[index]++;
                    boat.Respawned+=()=>rescues[index]++;
                }
                Physics.SyncTransforms();
                for(int step=0;step<maxSteps;step++)
                {
                    if(learned)Academy.Instance.EnvironmentStep();
                    bool all=true;
                    for(int i=0;i<boats.Length;i++)
                    {
                        if(progress[i].Finished)
                        {
                            boats[i].GetComponent<Rigidbody>().isKinematic=true;
                            foreach(var collider in boats[i].GetComponentsInChildren<Collider>())collider.enabled=false;
                            continue;
                        }
                        all=false;
                        if(!learned)boats[i].GetComponent<BoatAIInput>().SendMessage("FixedUpdate");
                        boats[i].SendMessage("FixedUpdate");
                    }
                    if(all)break;
                    Physics.Simulate(.02f);
                    foreach(var item in progress)item.Tick((step+1)*.02f);
                }
                int finished=0;
                var audit=new ObservationAudit{rows=new ObservationRow[boats.Length]};
                for(int i=0;i<boats.Length;i++)
                {
                    if(progress[i].Finished)finished++;
                    var policy=boats[i].GetComponent<BoatLearningAgent>();
                    if(policy!=null)
                    {
                        var sensor=new VectorSensor(BoatLearningAgent.ObservationCount);policy.CollectObservations(sensor);
                        var observations=((IEnumerable<float>)typeof(VectorSensor).GetMethod("GetObservations",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sensor,null)).ToArray();
                        var expert=boats[i].gameObject.AddComponent<BoatAIInput>();expert.Initialize(boats[i],i<2?i*2:(i-2)*2+1);expert.enabled=false;
                        expert.SendMessage("RejoinRoute");expert.SendMessage("FixedUpdate");
                        audit.rows[i]=new ObservationRow{observations=observations,modelAction=new[]{policy.Steer,policy.Throttle},expertAction=new[]{expert.Steer,expert.Throttle}};
                    }
                    report.AppendLine("hull="+boats[i].definition.name+" finished="+progress[i].Finished+" checkpoints="+progress[i].Checkpoints+" time="+progress[i].FinishTime.ToString("F2")+" distance="+progress[i].Distance.ToString("F2")+" impacts="+impacts[i]+" recoveries="+rescues[i]+" pos="+boats[i].transform.position+(policy!=null?" steer="+policy.Steer+" throttle="+policy.Throttle+" arc="+policy.CourseDistance:""));
                }
                string hash="";
                if(learned)
                    using(var sha=System.Security.Cryptography.SHA256.Create())
                        hash=System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(AssetDatabase.GetAssetPath(model)))).Replace("-","").ToLowerInvariant();
                report.Insert(0,"controller="+(learned?"PPO":"FSM")+" finished="+finished+"/6 limitSeconds="+(maxSteps*.02f)+" traffic=true model_sha256="+hash+" difficulty="+difficulty+" scenario="+scenario+"\n");
                Directory.CreateDirectory("Training/Reports");
                if(learned&&difficulty==2&&scenario==0)File.WriteAllText("Training/Reports/observation-audit.json",JsonUtility.ToJson(audit,true));
                string reportName="Training/Reports/full-race-"+(learned?"ppo":"fsm");
                File.WriteAllText(reportName+"-d"+difficulty+"-s"+scenario+".txt",report.ToString());
                if(difficulty==2&&scenario==0)File.WriteAllText(reportName+".txt",report.ToString());return report.ToString();
            }
            finally
            {
                foreach(var boat in boats)if(boat!=null)Object.DestroyImmediate(boat.gameObject);
                Physics.simulationMode=previous;Academy.Instance.AutomaticSteppingEnabled=automatic;
                foreach(var old in existing)if(old!=null)old.gameObject.SetActive(true);
            }
        }
    }
}
