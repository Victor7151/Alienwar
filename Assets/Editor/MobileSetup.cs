using UnityEditor;
using UnityEngine;

namespace Starfall.Editor
{
    public class MobileSetup
    {
        [MenuItem("Starfall/Setup Mobile Landscape")]
        public static void SetupLandscape()
        {
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            Debug.Log("[MobileSetup] Player Settings updated to strictly Landscape.");
        }
    }
}
