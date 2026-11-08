using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;
using Unity.MLAgents;

namespace VuotThac.EditorTools.Boats
{
    public static class PhaseFourValidation
    {
        public static string RunRace(int rosterGroup=-1)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Run in Play Mode");
            var screen=UnityEngine.Object.FindAnyObjectByType<BoatSelectScreen>();
            bool hadBoat=PlayerPrefs.HasKey("VuotThac.SelectedBoat"),hadBrain=PlayerPrefs.HasKey("VuotThac.LearnedAI");
            int oldBoat=PlayerPrefs.GetInt("VuotThac.SelectedBoat",0),oldBrain=PlayerPrefs.GetInt("VuotThac.LearnedAI",0);
            PlayerPrefs.SetInt("VuotThac.LearnedAI",1);screen.ReturnToSelection();screen.SendMessage("Show",(object)0);
            if(rosterGroup>=0)
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                ((CupSeries)typeof(BoatSelectScreen).GetField("cup",flags).GetValue(screen)).Start();
                var roster=rosterGroup==2?new[]{4,8,0,5,7}:rosterGroup==3?new[]{3,9,1,6,2}:new int[5];
                if(rosterGroup<2)for(int i=0;i<5;i++)roster[i]=rosterGroup*5+i;
                typeof(BoatSelectScreen).GetField("cupBoatRoster",flags).SetValue(screen,roster);
            }
            screen.SendMessage("StartRace");
            var race=screen.GetComponent<BoatRace>();
            race.Player.GetComponent<PlayerBoatInput>().enabled=false;
            BoatLearningAgent.Attach(race.Player.Boat,1,false,BoatBrainSettings.Model).SetDifficulty(DifficultyLevel.Normal);
            var mode=Physics.simulationMode;bool automatic=Academy.Instance.AutomaticSteppingEnabled;
            Physics.simulationMode=SimulationMode.Script;Academy.Instance.AutomaticSteppingEnabled=false;
            var report=new StringBuilder();
            try
            {
                foreach(var racer in race.Racers){racer.Boat.enabled=false;racer.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;}
                Physics.SyncTransforms();float nextUse=0;
                for(int step=0;step<21000;step++)
                {
                    Academy.Instance.EnvironmentStep();
                    foreach(var racer in race.Racers)
                    {
                        if(racer.Finished||racer.Withdrawn){racer.GetComponent<Rigidbody>().isKinematic=true;foreach(var c in racer.GetComponentsInChildren<Collider>())c.enabled=false;continue;}
                        racer.Boat.SendMessage("FixedUpdate");
                    }
                    Physics.Simulate(.02f);race.Tick(.02f);
                    if(race.Elapsed>nextUse)
                    {
                        nextUse=race.Elapsed+.5f;var item=race.Player.GetComponent<BoatItemState>();
                        bool safe=(bool)typeof(RaceItems).GetMethod("SafeToBoost",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(race.Items,new object[]{race.Player.Boat});
                        if(item.Held!=RaceItem.Boost||safe)item.Use();
                    }
                    if(race.Settled)break;
                }
                int completed=0,collected=0,used=0;
                foreach(var racer in race.Racers)
                {
                    var state=racer.GetComponent<BoatItemState>();if(racer.Finished)completed++;collected+=state.PickupsCollected;used+=state.ItemsUsed;
                    report.AppendLine(racer.RacerName+" hull="+racer.Boat.definition.name+" finished="+racer.Finished+" CP="+racer.Checkpoints+" time="+racer.FinishTime.ToString("F2")+" pickups="+state.PickupsCollected+" boosts="+state.BoostUses+" shields="+state.ShieldUses+" logs="+state.LogUses+" absorbed="+state.ShieldsConsumed);
                }
                report.Insert(0,"Items race: finished="+completed+"/6 elapsed="+race.Elapsed.ToString("F2")+" collected="+collected+" used="+used+" seed="+race.Items.Seed+" pickups="+race.Items.PickupCount+" obstacles="+race.Layout.ObstacleCount+" layout="+race.Layout.Signature+"\n");
                Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/phase4-race.txt",report.ToString());return report.ToString();
            }
            finally
            {
                screen.ReturnToSelection();screen.SendMessage("Show",(object)oldBoat);
                Physics.simulationMode=mode;Academy.Instance.AutomaticSteppingEnabled=automatic;
                if(hadBoat)PlayerPrefs.SetInt("VuotThac.SelectedBoat",oldBoat);else PlayerPrefs.DeleteKey("VuotThac.SelectedBoat");
                if(hadBrain)PlayerPrefs.SetInt("VuotThac.LearnedAI",oldBrain);else PlayerPrefs.DeleteKey("VuotThac.LearnedAI");PlayerPrefs.Save();
            }
        }
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Run in Play Mode");
            var screen=UnityEngine.Object.FindAnyObjectByType<BoatSelectScreen>();
            var type=typeof(BoatSelectScreen);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var report=new StringBuilder();int passed=0,total=0;
            Action<bool,string> check=(ok,name)=>{total++;if(ok)passed++;report.AppendLine(name+"="+ok);};
            Action<string> call=name=>screen.SendMessage(name);
            Action<float> tick=dt=>type.GetMethod("TickPhaseFour",flags).Invoke(screen,new object[]{dt});
            bool hadTime=PlayerPrefs.HasKey("VuotThac.BestTime"),hadCup=PlayerPrefs.HasKey("VuotThac.CupBestRank");
            float oldTime=PlayerPrefs.GetFloat("VuotThac.BestTime");int oldCup=PlayerPrefs.GetInt("VuotThac.CupBestRank");
            try
            {
                screen.ReturnToSelection();type.GetField("cupSelected",flags).SetValue(screen,false);call("StartRace");
                var race=screen.GetComponent<BoatRace>();var boat=race.Player.Boat;var item=boat.GetComponent<BoatItemState>();
                check(race.Items.PickupCount>10&&race.Racers.Count==6,"pickups and six inventories created");
                check(item.Give(RaceItem.Boost)&&!item.Give(RaceItem.Shield),"one inventory slot");
                check(!item.Use()&&item.Held==RaceItem.Boost,"countdown blocks use without consuming");
                race.Tick(3.1f);check(item.Use()&&item.Held==RaceItem.None&&item.BoostRemaining==2,"boost starts for two seconds");
                item.Tick(1.99f);check(item.BoostRemaining>0,"boost survives before two seconds");item.Tick(.02f);check(item.BoostRemaining==0,"boost expires");
                item.Give(RaceItem.Shield);item.Use();var body=boat.GetComponent<Rigidbody>();body.linearVelocity=Vector3.forward*8;
                boat.ApplyItemHit();check(item.ShieldsConsumed==1&&!item.ShieldActive&&body.linearVelocity.z==8,"shield consumes exactly one hit");
                boat.ApplyItemHit();check(Mathf.Abs(body.linearVelocity.z-3.6f)<.001f,"second unshielded hit slows boat");
                var sample=screen.track.EvaluateAtDistance(510);var position=screen.track.AfterRaceInsertion(sample.center);position.y=sample.WaterHeight+boat.FloatHeight;
                boat.Place(position,Quaternion.LookRotation(sample.tangent));item.Give(RaceItem.Log);check(item.Use()&&race.Items.ActiveLogs==1,"log dropped behind boat");
                var behind=position-sample.tangent*(boat.hullLength*.5f+2.2f);var target=race.Racers[1].Boat;target.Place(behind,Quaternion.LookRotation(sample.tangent));
                target.GetComponent<BoatItemState>().Give(RaceItem.Boost);boat.Place(behind,Quaternion.LookRotation(sample.tangent));
                var targetBody=target.GetComponent<Rigidbody>();targetBody.linearVelocity=sample.tangent*8;
                body.linearVelocity=sample.tangent*8;race.Items.Tick(.001f);
                float slowed=Vector3.ProjectOnPlane(targetBody.linearVelocity,Vector3.up).magnitude;
                check(Mathf.Abs(slowed-3.6f)<.01f,"log slows trailing boat");race.Items.Tick(.001f);
                check(Mathf.Abs(Vector3.ProjectOnPlane(targetBody.linearVelocity,Vector3.up).magnitude-slowed)<.001f,"same log does not hit twice");
                check(Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude>7.9f,"log owner immunity");
                race.Items.Tick(8.1f);check(race.Items.ActiveLogs==0,"logs expire and return to pool");
                item.ClearEffects();
                var list=(System.Collections.IList)typeof(RaceItems).GetField("pickups",flags).GetValue(race.Items);var pickup=list[0];
                var pickupPosition=(Vector3)pickup.GetType().GetField("position").GetValue(pickup);
                boat.Place(pickupPosition-Vector3.up*.7f,Quaternion.identity);race.Items.Tick(.01f);
                check(item.Held!=RaceItem.None,"actual pickup collection");
                screen.ReturnToSelection();check(race.Items==null&&race.Racers.Count==0,"return cleans race and items");
                type.GetField("cupSelected",flags).SetValue(screen,true);call("StartRace");
                var cup=(CupSeries)type.GetField("cup",flags).GetValue(screen);
                var cupHulls=new BoatDefinition[5];for(int i=0;i<5;i++)cupHulls[i]=race.Racers[i+1].Boat.definition;
                for(int round=1;round<=3;round++)
                {
                    bool sameRoster=true;for(int i=0;i<5;i++)sameRoster&=cupHulls[i]==race.Racers[i+1].Boat.definition;
                    check(sameRoster,"cup preserves opponent identities round "+round);
                    race.Tick(3.1f);
                    for(int i=0;i<6;i++)race.Racers[i].ApplyNetwork(new BoatNetworkState{finished=true,finishTime=100+round+i,finishOrder=i+1,checkpoints=12,distance=screen.track.FinishDistance});
                    call("FinishRace");tick(.1f);
                    check(cup.CompletedRounds==round&&cup.Points[0]==10*round,"cup round "+round+" committed");
                    tick(.1f);check(cup.Points[0]==10*round&&!cup.RecordRound(race,round),"round "+round+" cannot score twice");
                    if(round<3)call("ContinueCup");
                }
                check(cup.Complete&&cup.PlayerRank==1&&cup.Points[0]==30,"three-round cup champion");
                call("RestartPhaseFour");check(!cup.Complete&&cup.CompletedRounds==0&&race.Racers.Count==6&&race.Player.GetComponent<BoatItemState>().Held==RaceItem.None,"restart resets cup and inventory");
                race.Player.ApplyNetwork(new BoatNetworkState{finished=true,finishTime=130,finishOrder=1,checkpoints=12,distance=screen.track.FinishDistance});
                call("FinishRace");tick(.1f);check(cup.CompletedRounds==0,"cup waits for remaining boats");
                tick(45.1f);check(cup.CompletedRounds==1&&cup.Points[0]==10&&cup.Points[1]==0&&race.Racers[1].TimedOut,"45-second grace finalizes DNF without points");
                screen.ReturnToSelection();type.GetField("cupSelected",flags).SetValue(screen,false);call("StartRace");race.Tick(3.1f);race.Tick(421);tick(.1f);
                check(race.Player.TimedOut&&race.Settled&&!race.Running,"race timeout resolves unfinished boats");
                check(!race.Player.Finished,"timeout cannot award a finish");
                var noFinishCup=new CupSeries();noFinishCup.Start();for(int round=1;round<=3;round++)noFinishCup.RecordRound(race,round);
                check(noFinishCup.Complete&&noFinishCup.Points[0]==0&&noFinishCup.PlayerRank==6,"zero-finish cup cannot award first place");
            }
            finally
            {
                screen.ReturnToSelection();type.GetField("cupSelected",flags).SetValue(screen,false);
                if(hadTime)PlayerPrefs.SetFloat("VuotThac.BestTime",oldTime);else PlayerPrefs.DeleteKey("VuotThac.BestTime");
                if(hadCup)PlayerPrefs.SetInt("VuotThac.CupBestRank",oldCup);else PlayerPrefs.DeleteKey("VuotThac.CupBestRank");PlayerPrefs.Save();
            }
            report.Insert(0,"Phase four rules and UI: "+passed+"/"+total+"\n");Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/phase4-rules.txt",report.ToString());return report.ToString();
        }
    }
}
