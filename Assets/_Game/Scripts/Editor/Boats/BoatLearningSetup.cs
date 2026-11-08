using System.IO;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    [InitializeOnLoad]
    public static class BoatLearningSetup
    {
        static BoatLearningSetup()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode)ClearRequest();
            };
        }
        [MenuItem("Vuot Thac/AI/Record expert demonstrations")]
        public static void RecordDemonstrations()=>Prepare("demo");
        [MenuItem("Vuot Thac/AI/Connect PPO trainer")]
        public static void Train()=>Prepare("train");
        [MenuItem("Vuot Thac/AI/Record all boat types")]
        public static void RecordAllBoats()=>PrepareAllBoats("demo");
        [MenuItem("Vuot Thac/AI/Train all boat types")]
        public static void TrainAllBoats()=>PrepareAllBoats("train");
        static void PrepareAllBoats(string mode)
        {
            StartRequest(new BoatTrainingEnvironment.Request{mode=mode,allHulls=true,agents=mode=="demo"?20:10,demonstrationSteps=3000,demonstrationDirectory="Training/Demos-allboats"});
        }
        [MenuItem("Vuot Thac/AI/Stop training or recording")]
        public static void Stop()
        {
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            else ClearRequest();
        }
        public static void Prepare(string mode)
        {StartRequest(new BoatTrainingEnvironment.Request{mode=mode});}
        static void StartRequest(BoatTrainingEnvironment.Request request)
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first");
            Directory.CreateDirectory("Training");
            File.WriteAllText(BoatTrainingEnvironment.RequestFile,JsonUtility.ToJson(request));
            EditorApplication.isPlaying=true;
        }
        public static void ClearRequest()
        {
            var path=BoatTrainingEnvironment.RequestFile;
            if(File.Exists(path))File.Delete(path);
        }
    }
}
