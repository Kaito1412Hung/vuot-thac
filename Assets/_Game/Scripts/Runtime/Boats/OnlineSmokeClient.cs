#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace VuotThac.Boats
{
    // Explicit command-line integration test entrypoint; never runs during normal play.
    public sealed class OnlineSmokeClient : MonoBehaviour
    {
        [Serializable] class Report
        {
            public bool joined,started,moved,host;
            public int racers,checkpoints;
            public float elapsed,distanceMoved;
            public string status,error;
        }
        public static string Argument(string key)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);
            return i>=0&&i+1<args.Length?args[i+1]:null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {if(!string.IsNullOrEmpty(Argument("-online-test-join")))new GameObject("OnlineSmokeClient").AddComponent<OnlineSmokeClient>();}
        async void Start()
        {
            var report=new Report();OnlineBoatRoom room=null;
            try
            {
                await Task.Delay(1500);room=FindAnyObjectByType<OnlineBoatRoom>();
                await room.ConnectAsync(false,Argument("-online-test-join"),"Bạn kiểm tra",2);
                report.joined=room.Session!=null;report.host=room.IsHost;
                var until=DateTime.UtcNow.AddSeconds(60);
                while(room.Members.Count<2&&DateTime.UtcNow<until)await Task.Delay(100);
                room.ToggleReady();
                while(!room.Racing&&DateTime.UtcNow<until)await Task.Delay(100);
                report.started=room.Racing;
                var race=FindAnyObjectByType<BoatRace>();
                if(race!=null&&race.Player!=null)
                {
                    report.racers=race.Racers.Count;var input=race.Player.GetComponent<PlayerBoatInput>();
                    var start=race.Player.transform.position;input.TouchThrottle=.8f;
                    await Task.Delay(10000);input.ReleaseTouch();
                    report.elapsed=race.Elapsed;report.distanceMoved=Vector3.Distance(start,race.Player.transform.position);
                    report.moved=report.distanceMoved>3;report.checkpoints=race.Player.Checkpoints;
                }
                report.status=room.Status;
            }
            catch(Exception e){report.error=e.Message;}
            finally
            {
                string output=Argument("-online-test-report");
                if(!string.IsNullOrEmpty(output))File.WriteAllText(output,JsonUtility.ToJson(report,true));
                if(room!=null)await room.LeaveAsync();
                Application.Quit();
            }
        }
    }
}
#endif
