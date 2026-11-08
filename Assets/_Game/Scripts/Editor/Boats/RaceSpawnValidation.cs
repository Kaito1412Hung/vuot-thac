using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    public static class RaceSpawnValidation
    {
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run in Play Mode");
            var screen=Object.FindAnyObjectByType<BoatSelectScreen>();screen.ReturnToSelection();
            var original=new List<GameObject>();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if(filter.GetComponent<Collider>()!=null&&(filter.name.StartsWith("BranchRock_")||filter.transform.parent!=null&&filter.transform.parent.name=="RiverRocks"))original.Add(filter.gameObject);
            var session=new GameObject("SpawnValidation");var race=session.AddComponent<BoatRace>();
            int[] seeds=new int[5],items=new int[5],layouts=new int[5];int total=0,passed=0;
            var report=new StringBuilder();System.Action<bool,string> check=(ok,name)=>{total++;if(ok)passed++;report.AppendLine(name+"="+ok);};
            try
            {
                for(int test=0;test<5;test++)
                {
                    var boat=Object.Instantiate(screen.catalog.boats[0].prefab).GetComponent<BoatController>();boat.Track=screen.track;
                    try
                    {
                        int? seed=test<2?111:test==2?222:(int?)null;
                        race.Begin(boat,screen.catalog,screen.track,DifficultyLevel.Normal,seed);
                        seeds[test]=race.Items.Seed;items[test]=race.Items.PickupSignature;layouts[test]=race.Layout.Signature;
                        check(race.Items.PickupCount>=150&&race.Items.PickupCount<=240,"dense item count case "+test);
                        check(race.Layout.ObstacleCount>=15,"random obstacles case "+test);
                        var paths=new List<BoatCourse>();for(int i=0;i<4;i++)paths.Add(new BoatCourse(screen.track,i));
                        float clearance=float.MaxValue;
                        foreach(Transform child in race.Layout.transform)
                            foreach(var path in paths)
                            {
                                int segment=0;float lateral;float d=path.Nearest(child.position,ref segment,out lateral,true);
                                var delta=child.position-path.At(d,out _);delta.y=0;clearance=Mathf.Min(clearance,delta.magnitude);
                            }
                        check(clearance>=4.19f,"clear route corridor case "+test);
                        var channels=new HashSet<int>();var list=(IList)typeof(RaceItems).GetField("pickups",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(race.Items);
                        bool inWater=true;
                        foreach(var pickup in list)
                        {
                            var p=(Vector3)pickup.GetType().GetField("position").GetValue(pickup);int hint=-1;var water=screen.track.SampleWater(p,ref hint);
                            inWater&=water.inWater;channels.Add(water.channel);
                        }
                        check(inWater&&channels.Contains(0)&&channels.Contains(1)&&channels.Contains(2),"items cover water and all branches case "+test);
                        report.AppendLine("case="+test+" seed="+seeds[test]+" pickups="+race.Items.PickupCount+" obstacles="+race.Layout.ObstacleCount+" signature="+layouts[test]+" clearance="+clearance.ToString("F2"));
                    }
                    finally{race.Clear();Object.DestroyImmediate(boat.gameObject);}
                }
                check(items[0]==items[1]&&layouts[0]==layouts[1],"explicit seed reproduces layout");
                check(items[0]!=items[2]&&layouts[0]!=layouts[2],"different seeds change items and obstacles");
                check(seeds[3]!=seeds[4]&&items[3]!=items[4]&&layouts[3]!=layouts[4],"ordinary replay generates a fresh layout");
                bool restored=true;foreach(var go in original)restored&=go!=null&&go.activeSelf;check(restored&&original.Count>0,"original river rocks restored after cleanup");
            }
            finally{race.Clear();Object.DestroyImmediate(session);}
            report.Insert(0,"Random spawn validation: "+passed+"/"+total+"\n");Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/random-spawns.txt",report.ToString());return report.ToString();
        }
    }
}
