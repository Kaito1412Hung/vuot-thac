using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    public static class OpponentRosterValidation
    {
        public static string Run()
        {
            var screen=Object.FindAnyObjectByType<BoatSelectScreen>();var catalog=screen.catalog;
            var covered=new HashSet<int>();var arrangements=new HashSet<string>();bool valid=true,reproduces=true;
            for(int seed=0;seed<100;seed++)
            {
                var a=BoatRace.DrawOpponents(catalog,seed);var b=BoatRace.DrawOpponents(catalog,seed);
                var unique=new HashSet<int>();
                for(int i=0;i<5;i++){valid&=a[i]>=0&&a[i]<catalog.boats.Length&&unique.Add(a[i]);reproduces&=a[i]==b[i];covered.Add(a[i]);}
                arrangements.Add(string.Join(",",a));
            }
            var report=new StringBuilder();report.AppendLine("uniqueValidFive="+valid+" reproducibleSeed="+reproduces+" typesCovered="+covered.Count+"/"+catalog.boats.Length+" distinctLineups="+arrangements.Count+"/100");
            report.AppendLine("seed111="+string.Join(",",BoatRace.DrawOpponents(catalog,111)));report.AppendLine("seed222="+string.Join(",",BoatRace.DrawOpponents(catalog,222)));
            Directory.CreateDirectory("Builds/Validation");File.WriteAllText("Builds/Validation/opponent-rosters.txt",report.ToString());return report.ToString();
        }
    }
}
