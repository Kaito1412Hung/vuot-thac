using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;
using VuotThac.Track;

namespace VuotThac.EditorTools.Boats
{
    public static class PhaseTwoValidation
    {
        public static string RunRules()
        {
            var track=Object.FindAnyObjectByType<RiverTrack>();
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var go=Object.Instantiate(catalog.boats[0].prefab);var boat=go.GetComponent<BoatController>();boat.Track=track;
            var sample=track.EvaluateAtDistance(track.StartDistance-12);
            boat.Place(track.AfterRaceInsertion(sample.center)+Vector3.up*boat.FloatHeight,Quaternion.LookRotation(sample.tangent));
            var rb=go.GetComponent<Rigidbody>();rb.isKinematic=true;
            var progress=go.AddComponent<RaceProgress>();progress.Initialize(boat,"Test");
            var report=new StringBuilder();int passed=0,total=0;float elapsed=0;
            System.Action<bool,string> check=(ok,name)=>{total++;if(ok)passed++;report.AppendLine(name+"="+ok);};
            System.Action<float,float> move=(distance,altitude)=>
            {
                var s=track.EvaluateAtDistance(distance);
                boat.Place(track.AfterRaceInsertion(s.center)+Vector3.up*(boat.FloatHeight+altitude),Quaternion.LookRotation(s.tangent));progress.Tick(++elapsed);
            };
            try
            {
                move(track.StartDistance+1,0);check(progress.Checkpoints==1,"start gate");
                move(track.FinishDistance+1,0);progress.Tick(++elapsed);
                check(!progress.Finished&&progress.Checkpoints==1,"finish teleport rejected");
                move(100,0);move(110,0);move(130,0);move(145,8);move(145,0);
                check(progress.Checkpoints==1,"airborne gate rejected");
                move(130,0);move(145,0);check(progress.Checkpoints==2,"gate crossed in order");
                boat.Respawn();check(Mathf.Abs(progress.Distance-140)<.01f,"respawn retains last checkpoint");
                for(float d=150;d<track.FinishDistance;d+=10)move(d,0);
                move(track.FinishDistance+1,0);check(progress.Finished&&progress.Checkpoints==12,"all ordered gates finish");
                float finishTime=progress.FinishTime;progress.Tick(elapsed+100);
                check(progress.FinishTime==finishTime,"finish time immutable");
            }
            finally{Object.DestroyImmediate(go);}
            report.Insert(0,"Race rules passed="+passed+"/"+total+"\n");
            Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/phase2-rules.txt",report.ToString());return report.ToString();
        }
        public static string RunRace(int difficulty=2)
        {
            if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run in Play Mode");
            var track=Object.FindAnyObjectByType<RiverTrack>();
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(BoatFactory.CatalogPath);
            var existing=Object.FindObjectsByType<BoatController>(FindObjectsSortMode.None);
            foreach(var old in existing)old.gameObject.SetActive(false);
            var session=new GameObject("Phase2Validation");var race=session.AddComponent<BoatRace>();
            var sample=track.EvaluateAtDistance(track.StartDistance-12);
            var player=Object.Instantiate(catalog.boats[0].prefab).GetComponent<BoatController>();
            player.Track=track;player.Place(track.AfterRaceInsertion(sample.center)+Vector3.up*player.FloatHeight,Quaternion.LookRotation(sample.tangent));
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var report=new StringBuilder();int resets=0;float maxHeight=-100,minHeight=100;
            try
            {
                race.Begin(player,catalog,track,(DifficultyLevel)difficulty);player.gameObject.AddComponent<BoatAIInput>().Initialize(player,1);
                foreach(var racer in race.Racers)
                {
                    racer.Boat.enabled=false;racer.GetComponent<BoatAIInput>().enabled=false;
                    racer.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;
                    racer.Boat.Respawned+=()=>{resets++;report.AppendLine("recovery t="+race.Elapsed.ToString("F1")+" "+racer.RacerName+" reason="+racer.Boat.LastRecoveryReason);};
                }
                Physics.SyncTransforms();
                for(int step=0;step<12000;step++)
                {
                    foreach(var racer in race.Racers)
                    {
                        racer.GetComponent<BoatAIInput>().SendMessage("FixedUpdate");racer.Boat.SendMessage("FixedUpdate");
                    }
                    Physics.Simulate(.02f);race.Tick(.02f);
                    bool finished=true;
                    foreach(var racer in race.Racers)
                    {
                        int hint=-1;var water=track.SampleWater(racer.transform.position,ref hint);
                        float height=racer.transform.position.y-water.height;
                        maxHeight=Mathf.Max(maxHeight,height);minHeight=Mathf.Min(minHeight,height);
                        if(!racer.Finished)finished=false;
                    }
                    if(finished)break;
                }
                int completed=0;
                foreach(var racer in race.Racers)
                {
                    if(racer.Finished)completed++;
                    report.AppendLine(racer.RacerName+" finished="+racer.Finished+" rank="+race.RankOf(racer)+" CP="+racer.Checkpoints+" d="+racer.Distance.ToString("F1")+" state="+racer.GetComponent<BoatAIInput>().CurrentMode+" pos="+racer.transform.position+" speed="+racer.Boat.Speed.ToString("F1"));
                }
                report.Insert(0,"Finished="+completed+"/6 elapsed="+race.Elapsed.ToString("F1")+" resets="+resets+" waterClearance="+minHeight.ToString("F3")+".."+maxHeight.ToString("F3")+"\n");
                Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/phase2-difficulty-"+difficulty+".txt",report.ToString());
                if(difficulty==2)File.WriteAllText("Builds/Validation/phase2-race.txt",report.ToString());return report.ToString();
            }
            finally
            {
                race.Clear();Object.DestroyImmediate(player.gameObject);Object.DestroyImmediate(session);
                Physics.simulationMode=previous;
                foreach(var old in existing)if(old!=null)old.gameObject.SetActive(true);
            }
        }
    }
}
