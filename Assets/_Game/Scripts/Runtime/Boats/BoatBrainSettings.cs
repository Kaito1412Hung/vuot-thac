using UnityEngine;
using Unity.InferenceEngine;

namespace VuotThac.Boats
{
    public static class BoatBrainSettings
    {
        [System.Serializable] sealed class ModelInfo {public int observation_schema;public bool validated;}
        public static bool Validated
        {
            get
            {
                var metadata=Resources.Load<TextAsset>("AI/SuoiRungBoatInfo");
                if(metadata==null)return false;
                var info=JsonUtility.FromJson<ModelInfo>(metadata.text);
                return info!=null&&info.observation_schema==BoatLearningAgent.ObservationSchema&&info.validated;
            }
        }
        public static ModelAsset Model
        {
            get
            {
                var metadata=Resources.Load<TextAsset>("AI/SuoiRungBoatInfo");
                if(metadata==null)return null;
                var info=JsonUtility.FromJson<ModelInfo>(metadata.text);
                if(info==null||info.observation_schema!=BoatLearningAgent.ObservationSchema)return null;
                return Resources.Load<ModelAsset>("AI/SuoiRungBoat");
            }
        }
        public static bool UseModel
        {
            get=>PlayerPrefs.GetInt("VuotThac.LearnedAI",Validated?1:0)==1&&Model!=null;
            set{PlayerPrefs.SetInt("VuotThac.LearnedAI",value?1:0);PlayerPrefs.Save();}
        }
        public static string Label=>UseModel?(Validated?"AI: PPO":"AI: PPO (THỬ)"):"AI: FSM";
    }
}
