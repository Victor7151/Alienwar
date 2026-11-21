using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Starfall.Player;

namespace Starfall.Editor
{
    public static class FlameSetupUtility
    {
        private const string FlameFolder = "Assets/Art/VFX/flame";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/PlayerShip.prefab";

        [InitializeOnLoadMethod]
        [MenuItem("STARFALL/Setup Blender Flame Sprites & Thrusters")]
        public static void SetupBlenderFlameAnimation()
        {
            Debug.Log("[FlameSetupUtility] Configuring Blender flame frames...");

            var dir = new DirectoryInfo(FlameFolder);
            if (!dir.Exists)
            {
                Debug.LogError($"[FlameSetupUtility] Folder not found: {FlameFolder}");
                return;
            }

            var pngFiles = dir.GetFiles("*.png");
            System.Array.Sort(pngFiles, (a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));

            if (pngFiles.Length == 0)
            {
                Debug.LogError("[FlameSetupUtility] No PNG files found in flame folder!");
                return;
            }

            // 1. Configure TextureImporters for each frame
            var spriteList = new List<Sprite>();

            foreach (var file in pngFiles)
            {
                string assetPath = $"{FlameFolder}/{file.Name}";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    bool modified = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        modified = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        modified = true;
                    }
                    // Pivot at top-center (x: 0.5, y: 1.0) so the flame connects flush to nozzle
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.TopCenter || settings.spritePivot != new Vector2(0.5f, 1.0f))
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.TopCenter;
                        settings.spritePivot = new Vector2(0.5f, 1.0f);
                        importer.SetTextureSettings(settings);
                        modified = true;
                    }
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        modified = true;
                    }
                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        modified = true;
                    }
                    if (importer.filterMode != FilterMode.Bilinear)
                    {
                        importer.filterMode = FilterMode.Bilinear;
                        modified = true;
                    }
                    if (importer.wrapMode != TextureWrapMode.Clamp)
                    {
                        importer.wrapMode = TextureWrapMode.Clamp;
                        modified = true;
                    }
                    if (importer.spritePixelsPerUnit != 150)
                    {
                        importer.spritePixelsPerUnit = 150;
                        modified = true;
                    }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                    }
                }

                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (spr != null)
                {
                    spriteList.Add(spr);
                }
            }

            Debug.Log($"<color=cyan>[FlameSetupUtility] Successfully prepared {spriteList.Count} flame frames.</color>");

            // 2. Assign to PlayerShip.prefab
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var thrusters = root.GetComponent<ShipEngineThrusters>();
                if (thrusters != null)
                {
                    var so = new SerializedObject(thrusters);
                    so.Update();

                    var framesProp = so.FindProperty("customFlameFrames");
                    framesProp.arraySize = spriteList.Count;
                    for (int i = 0; i < spriteList.Count; i++)
                    {
                        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = spriteList[i];
                    }

                    var fpsProp = so.FindProperty("animationFps");
                    if (fpsProp != null) fpsProp.floatValue = 24f; // Smooth 24fps flame loop

                    so.ApplyModifiedProperties();
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log("<color=green>[FlameSetupUtility] Blender flame animation wired into PlayerShip.prefab!</color>");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
