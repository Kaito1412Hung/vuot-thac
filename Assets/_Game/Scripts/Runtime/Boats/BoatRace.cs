using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using VuotThac.Track;

namespace VuotThac.Boats
{
    public sealed class BoatRace : MonoBehaviour
    {
        public readonly List<RaceProgress> Racers=new List<RaceProgress>();
        public RaceProgress Player { get; private set; }
        public float Elapsed { get; private set; }
        public bool Running { get; private set; }
        public float CountdownRemaining { get; private set; }
        public int PlayerRank=>RankOf(Player);
        public DifficultyLevel Difficulty { get; private set; }
        int finishCount;
        float paceTick;
        public RaceItems Items {get;private set;}
        public RaceLayout Layout {get;private set;}
        public static int[] DrawOpponents(BoatCatalog catalog,int seed)
        {
            if(catalog==null||catalog.boats==null||catalog.boats.Length<5)throw new System.ArgumentException("Cần ít nhất 5 loại thuyền để chọn đối thủ.");
            var pool=new int[catalog.boats.Length];for(int i=0;i<pool.Length;i++)pool[i]=i;
            var random=new System.Random(seed);
            for(int i=pool.Length-1;i>0;i--){int j=random.Next(i+1);int value=pool[i];pool[i]=pool[j];pool[j]=value;}
            var chosen=new int[5];System.Array.Copy(pool,chosen,5);return chosen;
        }
        public bool Settled {get{foreach(var racer in Racers)if(!racer.Finished&&!racer.Withdrawn)return false;return Racers.Count>0;}}
        public void Conclude(){Running=false;foreach(var racer in Racers)if(racer!=null)racer.GetComponent<BoatItemState>()?.ClearEffects();}
        public void BeginFriends(IList<BoatController> boats,IList<string> names,int localIndex,RiverTrack track)
        {
            Clear();Elapsed=0;finishCount=0;
            float widest=0,longest=0;foreach(var boat in boats){widest=Mathf.Max(widest,boat.hullBeam);longest=Mathf.Max(longest,boat.hullLength);}
            float spacing=widest+2;
            int columns=Mathf.Min(boats.Count,Mathf.Max(1,Mathf.FloorToInt((track.width+track.appliedRaceStripWidth-4)/spacing)));
            for(int i=0;i<boats.Count;i++)
            {
                var boat=boats[i];var sample=track.EvaluateAtDistance(track.StartDistance-12-(i/columns)*(longest+3));
                int rowCount=Mathf.Min(columns,boats.Count-(i/columns)*columns);
                var position=track.AfterRaceInsertion(sample.center)+sample.right*((i%columns-(rowCount-1)*.5f)*spacing)-sample.tangent*boat.hullLength*.475f;
                position.y=sample.WaterHeight+boat.FloatHeight+.02f;boat.Place(position,Quaternion.LookRotation(sample.tangent));
                var progress=boat.gameObject.AddComponent<RaceProgress>();progress.Initialize(boat,names[i]);Racers.Add(progress);
                boat.GetComponent<Rigidbody>().isKinematic=true;if(i==localIndex)Player=progress;
            }
            CountdownRemaining=3;Running=true;
        }
        public void ApplyOnlineFrame(RoomPacket frame)
        {Elapsed=frame.time;CountdownRemaining=frame.countdown;}
        public void Begin(BoatController player,BoatCatalog catalog,RiverTrack track,DifficultyLevel difficulty=DifficultyLevel.Normal,int? itemSeed=null,IReadOnlyList<int> opponentRoster=null)
        {
            int seed=itemSeed??System.Guid.NewGuid().GetHashCode();
            int[] chosen;
            if(opponentRoster==null)chosen=DrawOpponents(catalog,seed^0x3A15C7);
            else
            {
                if(opponentRoster.Count!=5)throw new System.ArgumentException("Đội hình máy phải có 5 thuyền.");
                chosen=new int[5];var unique=new HashSet<int>();
                for(int i=0;i<5;i++)
                {
                    int id=opponentRoster[i];if(id<0||id>=catalog.boats.Length||!unique.Add(id))throw new System.ArgumentException("Loại thuyền máy không hợp lệ hoặc bị trùng.");
                    chosen[i]=id;
                }
            }
            Clear();Elapsed=0;finishCount=0;Difficulty=difficulty;
            var sample=track.EvaluateAtDistance(track.StartDistance-12);
            var line=track.AfterRaceInsertion(sample.center);
            float widest=player.hullBeam;
            for(int i=0;i<5;i++)widest=Mathf.Max(widest,catalog.boats[chosen[i]].prefab.GetComponent<BoatController>().hullBeam);
            float spacing=widest+2f;
            var rotation=Quaternion.LookRotation(sample.tangent,Vector3.up);
            // Six adjacent lanes, with every bow on the same transverse starting line.
            // The player takes an inner lane so both groups of opponents are visible.
            var playerPosition=line-sample.right*(spacing*.5f)-sample.tangent*(player.hullLength*.475f);
            playerPosition.y=sample.WaterHeight+player.FloatHeight+.02f;
            player.Place(playerPosition,rotation);
            Player=player.gameObject.AddComponent<RaceProgress>();Player.Initialize(player,"Bạn");Racers.Add(Player);
            for(int i=0;i<5;i++)
            {
                var def=catalog.boats[chosen[i]];
                int lane=i<2?i:i+1;
                var dimensions=def.prefab.GetComponent<BoatController>();
                var position=line+sample.right*((lane-2.5f)*spacing)-sample.tangent*(dimensions.hullLength*.475f);
                position.y=sample.WaterHeight+dimensions.FloatHeight+.02f;
                var go=Instantiate(def.prefab,position,rotation);go.name="AI_"+(i+1)+"_"+def.name;
                var boat=go.GetComponent<BoatController>();boat.Track=track;
                boat.Place(position,rotation);
                var progress=go.AddComponent<RaceProgress>();progress.Initialize(boat,"Đối thủ "+(i+1));Racers.Add(progress);
                int routeChoice=i<2?i*2:(i-2)*2+1;
                if(BoatBrainSettings.UseModel)
                    BoatLearningAgent.Attach(boat,routeChoice,false,BoatBrainSettings.Model).SetDifficulty(difficulty);
                else{var ai=go.AddComponent<BoatAIInput>();ai.Initialize(boat,routeChoice);ai.ConfigureDifficulty(difficulty);}
            }
            CountdownRemaining=3;
            foreach(var racer in Racers)racer.GetComponent<Rigidbody>().isKinematic=true;
            Running=true;
            var layoutRoot=new GameObject("RaceObstacleLayout");Layout=layoutRoot.AddComponent<RaceLayout>();Layout.Initialize(track,seed);
            Physics.SyncTransforms();
            var arena=new GameObject("RaceItems");Items=arena.AddComponent<RaceItems>();Items.Initialize(this,track,seed);
        }
        public void Tick(float dt)
        {
            if(!Running)return;
            if(Keyboard.current!=null&&Keyboard.current.f3Key.wasPressedThisFrame)
                foreach(var racer in Racers){var ai=racer.GetComponent<BoatAIInput>();if(ai!=null)ai.DebugSensors=!ai.DebugSensors;}
            if(CountdownRemaining>0)
            {
                CountdownRemaining=Mathf.Max(0,CountdownRemaining-dt);
                if(CountdownRemaining<=0)foreach(var racer in Racers)if(!racer.Withdrawn)racer.GetComponent<Rigidbody>().isKinematic=false;
                return;
            }
            Elapsed+=dt;
            if(Items!=null)Items.Tick(dt);
            foreach(var racer in Racers)
            {
                racer.Tick(Elapsed);
                if(racer.Finished&&racer.FinishOrder==0)
                {
                    racer.FinishOrder=++finishCount;
                    var ai=racer.GetComponent<BoatAIInput>();if(ai!=null)ai.CrossedFinish();
                }
            }
            paceTick-=dt;
            if(paceTick<=0)
            {
                paceTick=.5f;
                foreach(var racer in Racers)
                {
                    var ai=racer.GetComponent<BoatAIInput>();if(ai==null)continue;
                    // Bounded throttle adjustment only: no teleporting, speed edits or checkpoint skips.
                    float gap=Player.Distance-racer.Distance;
                    float pace=RaceDifficulty.Pace(Difficulty);
                    ai.Pace=Mathf.Clamp(pace+gap*.0015f,Mathf.Max(.3f,pace-.06f),Mathf.Min(1,pace+.06f));
                }
            }
        }
        public int RankOf(RaceProgress target)
        {
            if(target==null)return 0;int rank=1;
            foreach(var other in Racers)
            {
                if(other==target)continue;
                if(other.Withdrawn&&!target.Withdrawn)continue;
                if(target.Withdrawn&&!other.Withdrawn){rank++;continue;}
                bool ahead=target.Finished?other.Finished&&other.FinishOrder>0&&other.FinishOrder<target.FinishOrder:
                    other.Finished||other.Checkpoints>target.Checkpoints||(other.Checkpoints==target.Checkpoints&&other.Distance>target.Distance+.01f);
                if(!target.Finished&&!other.Finished&&other.Withdrawn==target.Withdrawn&&other.Checkpoints==target.Checkpoints&&Mathf.Abs(other.Distance-target.Distance)<=.01f&&Racers.IndexOf(other)<Racers.IndexOf(target))ahead=true;
                if(ahead)rank++;
            }
            return rank;
        }
        public string Results()
        {
            var ordered=new List<RaceProgress>(Racers);ordered.Sort((a,b)=>RankOf(a).CompareTo(RankOf(b)));
            var text=new StringBuilder();foreach(var racer in ordered)
                text.AppendLine(RankOf(racer)+". "+racer.RacerName+" — "+(racer.Withdrawn?(racer.TimedOut?"hết giờ":"mất kết nối"):racer.Finished?$"{Mathf.FloorToInt(racer.FinishTime/60):00}:{racer.FinishTime%60:00.0}":"đang đua"));
            return text.ToString();
        }
        public void Clear()
        {
            Running=false;CountdownRemaining=0;
            if(Layout!=null){Layout.Restore();Layout.gameObject.SetActive(false);Destroy(Layout.gameObject);Layout=null;}
            if(Items!=null)
            {
                foreach(var racer in Racers)if(racer!=null){var state=racer.GetComponent<BoatItemState>();if(state!=null){state.ClearEffects();Destroy(state);}}
                Items.gameObject.SetActive(false);Destroy(Items.gameObject);Items=null;
            }
            foreach(var racer in Racers)if(racer!=null){if(racer==Player)Destroy(racer);else{racer.gameObject.SetActive(false);Destroy(racer.gameObject);}}
            Racers.Clear();Player=null;
        }
        void OnDestroy(){Clear();}
    }
}
