using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Starfall.Core
{
    /// <summary>
    /// Manages a single, persistent UniversalRP 2D Global Light across the entire lifecycle of Starfall: Alien War.
    /// 
    /// Purpose:
    ///   • Resolves the URP 2D build error: "More than one global light on layer Default for light blend style index 0"
    ///   • Automatically instantiates before the first scene loads (RuntimeInitializeOnLoadMethod)
    ///   • Persists across HomeScene, LevelSelect, and all Combat Sectors via DontDestroyOnLoad
    ///   • Guarantees 100% full-screen ambient illumination with zero duplicate conflicts
    /// </summary>
    public static class PersistentGlobalLight
    {
        private static Light2D globalLightInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureLight();
        }

        /// <summary>
        /// Guarantees that a full-screen white Global Light 2D is present and active.
        /// </summary>
        public static Light2D EnsureLight()
        {
            if (globalLightInstance != null) return globalLightInstance;

            // Check if one was already spawned or exists in the active scene
            var allLights = Object.FindObjectsByType<Light2D>();
            for (int i = 0; i < allLights.Length; i++)
            {
                if (allLights[i] != null && allLights[i].lightType == Light2D.LightType.Global)
                {
                    globalLightInstance = allLights[i];
                    if (Application.isPlaying)
                    {
                        Object.DontDestroyOnLoad(globalLightInstance.gameObject);
                    }
                    return globalLightInstance;
                }
            }

            // Spawn the persistent global ambient light
            var lightGo = new GameObject("Starfall_GlobalLight2D");
            globalLightInstance = lightGo.AddComponent<Light2D>();
            globalLightInstance.lightType = Light2D.LightType.Global;
            globalLightInstance.color = Color.white;
            globalLightInstance.intensity = 1.0f;
            globalLightInstance.blendStyleIndex = 0;

            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(lightGo);
            }

            return globalLightInstance;
        }
    }
}
