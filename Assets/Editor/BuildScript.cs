using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Starfall.Editor
{
    public class BuildScript
    {
        [MenuItem("Starfall/Build Android APK")]
        public static void BuildAndroid()
        {
            // Ensure Landscape orientation
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            // Diagnostics Data debug symbols should be set manually in Player Settings in Unity 6+

            BuildPipeline.BuildPlayer(scenes, "Builds/Alienwar.apk", BuildTarget.Android, BuildOptions.None);
            Debug.Log("[BuildScript] Android APK Build Complete: Builds/Alienwar.apk");
        }
    }
}
