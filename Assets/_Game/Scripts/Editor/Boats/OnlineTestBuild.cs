using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace VuotThac.EditorTools.Boats
{
    public static class OnlineTestBuild
    {
        public static void Queue(bool scriptsOnly=false)
        {
            EditorApplication.delayCall+=()=>
            {
                const string folder="Builds/Windows/OnlineTest";Directory.CreateDirectory(folder);
                File.WriteAllText(folder+"/status.txt","building");
                try
                {
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes=new[]{"Assets/_Game/Scenes/Track01_SuoiRung.unity"},
                        locationPathName=folder+"/VuotThac.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development|(scriptsOnly?BuildOptions.BuildScriptsOnly:BuildOptions.None)
                    });
                    File.WriteAllText(folder+"/status.txt",report.summary.result+" errors="+report.summary.totalErrors);
                }
                catch(System.Exception e){File.WriteAllText(folder+"/status.txt",e.ToString());}
            };
        }
    }
}
