using UnityEngine;

namespace VuotThac.Boats
{
    public enum DifficultyLevel { VeryEasy, Easy, Normal, Hard, VeryHard }
    public static class RaceDifficulty
    {
        public static readonly string[] Names={"Rất dễ","Dễ","Thường","Khó","Rất khó"};
        static readonly float[] Paces={.64f,.73f,.82f,.91f,1f};
        static readonly float[] Reactions={.14f,.12f,.1f,.075f,.05f};
        static readonly float[] Junctions={5f,5f,5f,5.4f,5.8f};
        public static DifficultyLevel Saved=>(DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt("VuotThac.Difficulty",2),0,4);
        public static float Pace(DifficultyLevel level)=>Paces[(int)level];
        public static float Reaction(DifficultyLevel level)=>Reactions[(int)level];
        public static float JunctionSpeed(DifficultyLevel level)=>Junctions[(int)level];
    }
}
