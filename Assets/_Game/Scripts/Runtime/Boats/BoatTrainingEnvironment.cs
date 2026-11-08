using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Demonstrations;

namespace VuotThac.Boats
{
    public sealed class BoatTrainingEnvironment : MonoBehaviour
    {
        [Serializable] public class Request
        {
            public string mode="train";public int agents=8,demonstrationSteps=6000,seed=42;public float timeScale=20;
            public bool allHulls;public string demonstrationDirectory="Training/Demos-v4";
        }
        Request request;
        BoatLearningAgent[] agents;
        float nextStatus;
        float previousScale,previousFixed,previousMaximum;
        int previousFrameRate,previousVsync;
        bool previousBackground;
        public static string RequestFile=>Path.Combine(Application.dataPath,"../Training/editor-request.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Request options=null;
#if UNITY_EDITOR
            if(File.Exists(RequestFile))options=JsonUtility.FromJson<Request>(File.ReadAllText(RequestFile));
#endif
            if(Environment.GetCommandLineArgs().Contains("-boat-training"))options=new Request();
            if(options==null)return;
            var environment=new GameObject("BoatTrainingEnvironment").AddComponent<BoatTrainingEnvironment>();environment.StartTraining(options);
        }
        public void StartTraining(Request options)
        {
            request=options;
            previousScale=Time.timeScale;previousFixed=Time.fixedDeltaTime;previousMaximum=Time.maximumDeltaTime;
            previousFrameRate=Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;previousBackground=Application.runInBackground;
            foreach(var screen in FindObjectsByType<BoatSelectScreen>(FindObjectsSortMode.None))screen.enabled=false;
            foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(component.GetType().Namespace=="VuotThac.World"||component is BoatCamera||component is MobileSceneryBudget)component.enabled=false;
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.enabled=false;
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))renderer.enabled=false;
            Time.timeScale=options.timeScale;Time.fixedDeltaTime=.02f;Time.maximumDeltaTime=1;
            Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            var track=FindAnyObjectByType<VuotThac.Track.RiverTrack>();
            var screenSource=FindAnyObjectByType<BoatSelectScreen>(FindObjectsInactive.Include);
            var catalog=screenSource.catalog;
            agents=new BoatLearningAgent[options.agents];
            bool demonstrations=options.mode=="demo";
            for(int i=0;i<agents.Length;i++)
            {
                // Train on multiple hulls, including the narrow kayak and motorboat.
                var definition=catalog.boats[options.allHulls?i%catalog.boats.Length:(i*2)%catalog.boats.Length];
                var go=Instantiate(definition.prefab,Vector3.up*100,Quaternion.identity);go.name="LearningBoat_"+i;
                var boat=go.GetComponent<BoatController>();boat.Track=track;
                if(boat.model!=null)boat.model.gameObject.SetActive(false);
                var effects=go.GetComponent<BoatWaterEffects>();if(effects!=null)effects.enabled=false;
                var clip=go.GetComponent<BoatInteriorWaterClip>();if(clip!=null)clip.enabled=false;
                int choice=options.allHulls?(i<catalog.boats.Length?i:i+1):i<2?i*2:(i-2)*2+1;
                agents[i]=BoatLearningAgent.Attach(boat,choice,true,null,demonstrations,options.seed+i*137);
                if(demonstrations)
                {
                    var recorder=go.AddComponent<DemonstrationRecorder>();
                    recorder.DemonstrationName="BoatExpert"+i;recorder.DemonstrationDirectory=Path.GetFullPath(options.demonstrationDirectory);
                    recorder.Record=true;recorder.NumStepsToRecord=options.demonstrationSteps;
                }
            }
            for(int i=0;i<agents.Length;i++)for(int j=i+1;j<agents.Length;j++)
                Physics.IgnoreCollision(agents[i].GetComponent<Collider>(),agents[j].GetComponent<Collider>());
            Directory.CreateDirectory("Training");WriteStatus();
            if(!demonstrations&&!Academy.Instance.IsCommunicatorOn)Debug.LogError("PPO trainer is not connected. Start Training/train.ps1 before Play Mode.");
        }
        void Update()
        {
            if(request==null)return;
            if(Time.realtimeSinceStartup>nextStatus){nextStatus=Time.realtimeSinceStartup+5;WriteStatus();}
        }
        void WriteStatus()
        {
            var text=new System.Text.StringBuilder();
            text.AppendLine("mode="+request.mode+" connected="+Academy.Instance.IsCommunicatorOn+" academyStep="+Academy.Instance.StepCount);
            foreach(var agent in agents)if(agent!=null)text.AppendLine(agent.name+" episodes="+agent.Episodes+" success="+agent.Successes+" lastProgress="+agent.LastEpisodeProgress.ToString("F3"));
            File.WriteAllText("Training/runtime-status.txt",text.ToString());
        }
        void OnDestroy()
        {
            if(request==null)return;
            Time.timeScale=previousScale;Time.fixedDeltaTime=previousFixed;Time.maximumDeltaTime=previousMaximum;
            Application.targetFrameRate=previousFrameRate;QualitySettings.vSyncCount=previousVsync;Application.runInBackground=previousBackground;
        }
    }
}
