using UnityEngine;
using UnityEditor;
using Starfall.VFX;

namespace Starfall.Editor
{
    public static class ShockwaveVFXSetup
    {
        private const string PrefabPath = "Assets/Resources/VFX/ShockwaveVFX.prefab";

        [InitializeOnLoadMethod]
        [MenuItem("STARFALL/Setup Refractive Shockwave VFX Prefab")]
        public static void EnsureShockwavePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var comp = existing.GetComponent<ShockwaveVFX>();
                if (comp != null) return; // Already configured
            }

            var go = new GameObject("ShockwaveVFX");
            var shockwave = go.AddComponent<ShockwaveVFX>();
            shockwave.EnsureHierarchy();

            // Ensure destination folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/VFX"))
                AssetDatabase.CreateFolder("Assets/Resources", "VFX");

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShockwaveVFXSetup] Successfully generated {PrefabPath}");
        }
    }
}
