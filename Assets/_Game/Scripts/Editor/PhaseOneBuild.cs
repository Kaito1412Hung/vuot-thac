using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VuotThac.EditorTools
{
    public static class PhaseOneBuild
    {
        public static string Output => Path.GetFullPath("Builds/Android/Phase1");
        [MenuItem("Vuot Thac/Phase 1/Build Android APK")]
        public static void Queue()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "build-status.txt"), "Queued " + DateTime.Now);
            EditorApplication.delayCall += Build;
        }
        static void Build()
        {
            try
            {
                // Keep Gradle downloads, Android user settings and temp build files inside this project on D.
                var cache = Path.GetFullPath("Builds/Android/ToolCache");
                Directory.CreateDirectory(cache);
                var gradleCache = Path.GetFullPath("../.g");
                Directory.CreateDirectory(gradleCache);
                Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleCache);
                foreach (var name in new[] { "ANDROID_USER_HOME", "TEMP", "TMP" })
                {
                    var folder = Path.Combine(cache, name); Directory.CreateDirectory(folder);
                    Environment.SetEnvironmentVariable(name, folder);
                }
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(Output, "build-status.txt"), "Building " + DateTime.Now);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/_Game/Scenes/Track01_SuoiRung.unity" },
                    locationPathName = Path.Combine(Output, "VuotThac-Phase1.apk"),
                    target = BuildTarget.Android, options = BuildOptions.None
                });
                File.WriteAllText(Path.Combine(Output, "build-status.txt"), report.summary.result + "\nBytes=" + report.summary.totalSize + "\nTime=" + report.summary.totalTime + "\nErrors=" + report.summary.totalErrors);
                var log = new System.Text.StringBuilder();
                foreach (var step in report.steps) foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning) log.AppendLine(m.type + ": " + m.content);
                File.WriteAllText(Path.Combine(Output, "build-messages.txt"), log.ToString());
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Output, "build-status.txt"), e.ToString()); Debug.LogException(e); }
            finally
            {
                // Build Android làm Editor ở lại nền tảng Android; khi đó Play Mode dùng chất lượng Mobile
                // (80% độ phân giải, không khử răng cưa) nên hình bị mờ. Build xong thì trả về Windows.
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    EditorApplication.delayCall += () => EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }
        }
    }
}
