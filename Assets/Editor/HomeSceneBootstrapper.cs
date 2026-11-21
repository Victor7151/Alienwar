using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Starfall.Editor
{
    /// <summary>
    /// Ensures that whenever the player clicks Play in the Unity Editor,
    /// the game always boots from the Home Scene (Central Command Hub).
    /// Also adds convenient menu shortcuts to open and launch HomeScene.
    /// </summary>
    [InitializeOnLoad]
    public static class HomeSceneBootstrapper
    {
        public const string HOME_SCENE_PATH = "Assets/Scenes/HomeScene.unity";

        static HomeSceneBootstrapper()
        {
            SetPlayModeStartScene();
        }

        public static void SetPlayModeStartScene()
        {
            var homeSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(HOME_SCENE_PATH);
            if (homeSceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = homeSceneAsset;
            }
        }

        [MenuItem("STARFALL/Launch Game (Home Hub) _F5", priority = 0)]
        public static void LaunchGameFromHome()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(HOME_SCENE_PATH);
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("STARFALL/Open Home Scene", priority = 1)]
        public static void OpenHomeScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(HOME_SCENE_PATH);
            }
        }
    }
}
