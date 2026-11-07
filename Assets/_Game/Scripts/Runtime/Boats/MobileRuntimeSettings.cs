using UnityEngine;

namespace VuotThac.Boats
{
    public static class MobileRuntimeSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (!Application.isMobilePlatform) return;
            UnityEngine.InputSystem.InputSystem.settings.compensateForScreenOrientation = true;
            QualitySettings.SetQualityLevel(0, true);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
        }
    }
}
