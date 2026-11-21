using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Starfall.VFX;

namespace Starfall.Editor
{
    [InitializeOnLoad]
    public static class BackgroundMotionSetup
    {
        private const string BG_RESOURCE_DIR = "Assets/Resources/Backgrounds";

        // Previously ran on EVERY script reload, opening and re-saving all level scenes (slow, and it
        // interrupted Play). Now run it manually from STARFALL > Backgrounds when backgrounds change.
        static BackgroundMotionSetup() { }

        [MenuItem("STARFALL/Backgrounds/Setup Vertical (Top-to-Down) Backgrounds", priority = 40)]
        public static void ConfigureAllBackgroundsForTopToDown()
        {
            ConfigureTextureImporters();
            SetupParallaxInAllScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[BackgroundMotionSetup] Successfully configured all new backgrounds for top-to-down vertical scrolling!</color>");
        }

        public static void ConfigureTextureImporters()
        {
            string[] bgFiles = new[]
            {
                "Orbital Eclipse & Dark Atmospheric Horizon (Orbit Sector).png",
                "Abyssal Asteroid Field in Deep Shadow (Asteroid Belt _ Sector 1).png",
                "Dark Obsidian Deep Nebula (Mystic _ Sector 2).png",
                "War-Torn Wreckage & Dark Ash Sector (Hostile _ Sector 3).png",
                "Alien Bio-Mechanical Abyssal Hive (Alien Core Sector).png"
            };

            foreach (var file in bgFiles)
            {
                string path = $"{BG_RESOURCE_DIR}/{file}";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }

                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    if (importer.wrapMode != TextureWrapMode.Repeat)
                    {
                        importer.wrapMode = TextureWrapMode.Repeat;
                        changed = true;
                    }
                    if (importer.alphaIsTransparency != true)
                    {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }
                    if (importer.mipmapEnabled != false)
                    {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }
                    if (importer.maxTextureSize < 2048)
                    {
                        importer.maxTextureSize = 2048;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        Debug.Log($"[BackgroundMotionSetup] Configured TextureImporter for: {file}");
                    }
                }
            }
        }

        public static void SetupParallaxInAllScenes()
        {
            string s1Path = $"{BG_RESOURCE_DIR}/Orbital Eclipse & Dark Atmospheric Horizon (Orbit Sector).png";
            string s2Path = $"{BG_RESOURCE_DIR}/Abyssal Asteroid Field in Deep Shadow (Asteroid Belt _ Sector 1).png";
            string s3Path = $"{BG_RESOURCE_DIR}/Dark Obsidian Deep Nebula (Mystic _ Sector 2).png";
            string s4Path = $"{BG_RESOURCE_DIR}/War-Torn Wreckage & Dark Ash Sector (Hostile _ Sector 3).png";
            string s5Path = $"{BG_RESOURCE_DIR}/Alien Bio-Mechanical Abyssal Hive (Alien Core Sector).png";
            string starsPath = $"{BG_RESOURCE_DIR}/Starfield.png";
            if (!File.Exists(starsPath)) starsPath = "Assets/Art/Sprites/Backgrounds/Stars-Big_1.png";

            var s1 = AssetDatabase.LoadAssetAtPath<Sprite>(s1Path);
            var s2 = AssetDatabase.LoadAssetAtPath<Sprite>(s2Path);
            var s3 = AssetDatabase.LoadAssetAtPath<Sprite>(s3Path);
            var s4 = AssetDatabase.LoadAssetAtPath<Sprite>(s4Path);
            var s5 = AssetDatabase.LoadAssetAtPath<Sprite>(s5Path);
            var stars = AssetDatabase.LoadAssetAtPath<Sprite>(starsPath);

            string[] scenePaths = new[]
            {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Scenes/Level1_EarthOrbit.unity",
                "Assets/Scenes/Level2_AsteroidBelt.unity",
                "Assets/Scenes/Level3_DeepNebula.unity",
                "Assets/Scenes/Level4_WarTornWreckage.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath);
                var bgGo = GameObject.Find("ParallaxBackground");
                if (bgGo == null)
                {
                    bgGo = new GameObject("ParallaxBackground");
                }

                var controller = bgGo.GetComponent<ParallaxController>();
                if (controller == null) controller = bgGo.AddComponent<ParallaxController>();
                controller.DefaultDirection = ParallaxDirection.TopToBottom;

                // Explicitly serialize top-to-bottom settings into scene YAML
                var so = new SerializedObject(controller);
                var propDir = so.FindProperty("defaultDirection");
                if (propDir != null) propDir.enumValueIndex = (int)ParallaxDirection.TopToBottom;
                var propH = so.FindProperty("tileHeight");
                if (propH != null) propH.floatValue = 11.25f;
                var propW = so.FindProperty("worldWidth");
                if (propW != null) propW.floatValue = 20f;
                so.ApplyModifiedProperties();

                // Configure vertical parallax layers (worldWidth = 20, tileHeight = 11.25)
                EnsureParallaxLayer(bgGo.transform, "Layer_FarNebula", s1, 0.65f, -25, 20f, 11.25f);
                EnsureParallaxLayer(bgGo.transform, "Layer_MidStars", stars, 1.85f, -20, 20f, 11.25f);
                EnsureParallaxLayer(bgGo.transform, "Layer_NearStars", stars, 4.2f, -15, 20f, 11.25f);

                controller.ConfigureSectorSprites(s1, s2, s3, s4, s5, stars);
                controller.CacheLayersAndRenderers();

                int sector = 1;
                if (scenePath.Contains("Level4") || scenePath.Contains("WarTorn")) sector = 4;
                else if (scenePath.Contains("Level3") || scenePath.Contains("Nebula")) sector = 3;
                else if (scenePath.Contains("Level2") || scenePath.Contains("Asteroid")) sector = 2;
                controller.ApplySector(sector);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void EnsureParallaxLayer(Transform parent, string layerName, Sprite sprite, float speed, int sortingOrder, float width, float height)
        {
            var layerT = parent.Find(layerName);
            GameObject layerGo;
            if (layerT == null)
            {
                layerGo = new GameObject(layerName);
                layerGo.transform.SetParent(parent, false);
            }
            else
            {
                layerGo = layerT.gameObject;
            }

            // For Top-To-Down vertical motion: Tile A at (0, 0, 0), Tile B at (0, height, 0)
            var tileA = GetOrCreateTile($"{layerName}_A", layerGo.transform, sprite, 0f, 0f, sortingOrder, width, height);
            var tileB = GetOrCreateTile($"{layerName}_B", layerGo.transform, sprite, 0f, height, sortingOrder, width, height);

            var pl = layerGo.GetComponent<ParallaxLayer>();
            if (pl == null) pl = layerGo.AddComponent<ParallaxLayer>();
            pl.Initialize(tileA, tileB, width, height, speed, ParallaxDirection.TopToBottom);

            var soPl = new SerializedObject(pl);
            var plDir = soPl.FindProperty("direction");
            if (plDir != null) plDir.enumValueIndex = (int)ParallaxDirection.TopToBottom;
            var plH = soPl.FindProperty("tileHeight");
            if (plH != null) plH.floatValue = height;
            var plW = soPl.FindProperty("tileWidth");
            if (plW != null) plW.floatValue = width;
            soPl.ApplyModifiedProperties();
        }

        private static Transform GetOrCreateTile(string name, Transform parent, Sprite sprite, float offsetX, float offsetY, int sortingOrder, float width, float height)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = t.gameObject;
            }

            go.transform.localPosition = new Vector3(offsetX, offsetY, 0f);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            if (sprite != null)
            {
                float spriteW = sprite.rect.width > 0 ? sprite.rect.width / sprite.pixelsPerUnit : 1f;
                float spriteH = sprite.rect.height > 0 ? sprite.rect.height / sprite.pixelsPerUnit : 1f;
                go.transform.localScale = new Vector3(width / spriteW, height / spriteH, 1f);
            }

            return go.transform;
        }
    }
}
