using System.IO;
using File = System.IO.File;
using Directory = System.IO.Directory;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Rive.Components;
using Rive;
using Starfall.UI;
using Starfall.UI.Rive;

namespace Starfall.Editor
{
    [InitializeOnLoad]
    public static class RiveHealthBarFix
    {
        private const string RiveScriptedImporterBlock =
@"ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 11500000, guid: 043c4b240e8f94f828b8b81fa436f7f0, type: 3}";

        static RiveHealthBarFix()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    FixAllRiveMetaFiles(false);
                    FixActiveSceneRiveHUD(false);
                }
            };
        }

        [MenuItem("STARFALL/Rive/Fix & Setup Health Bar HUD", priority = 45)]
        public static void FixFromMenu()
        {
            FixAllRiveMetaFiles(true);
            FixActiveSceneRiveHUD(true);
        }

        [MenuItem("STARFALL/Rive/Import All Rive Assets (.meta fix)", priority = 46)]
        public static void ImportAllRiveAssets()
        {
            FixAllRiveMetaFiles(true);
        }

        public static void FixAllRiveMetaFiles(bool verbose)
        {
            string[] searchDirs = { "Assets/Resources/Rive", "Assets/new rive", "Assets/ALienware 2D assets/Rive" };
            int fixedCount = 0;

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.riv"))
                {
                    string metaPath = file + ".meta";
                    if (File.Exists(metaPath))
                    {
                        string metaContent = File.ReadAllText(metaPath);
                        if (!metaContent.Contains("ScriptedImporter"))
                        {
                            // Append the Rive ScriptedImporter block so Unity routes it to Rive.AssetImporter
                            File.AppendAllText(metaPath, "\n" + RiveScriptedImporterBlock + "\n");
                            string assetPath = file.Replace('\\', '/');
                            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                            fixedCount++;
                            if (verbose) Debug.Log($"[RiveHealthBarFix] Assigned Rive ScriptedImporter to {file}");
                        }
                    }
                }
            }

            // Also ensure alienwarhud.riv is copied into Assets/Resources/Rive for Resources.Load convenience
            string srcRiv = "Assets/new rive/alienwarhud.riv";
            string destRiv = "Assets/Resources/Rive/alienwarhud.riv";
            if (File.Exists(srcRiv) && !File.Exists(destRiv))
            {
                File.Copy(srcRiv, destRiv, true);
                File.WriteAllText(destRiv + ".meta", File.ReadAllText(srcRiv + ".meta"));
                AssetDatabase.ImportAsset(destRiv, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (verbose) Debug.Log("[RiveHealthBarFix] Mirrored alienwarhud.riv to Assets/Resources/Rive/alienwarhud.riv");
            }

            if (verbose || fixedCount > 0)
            {
                Debug.Log($"[RiveHealthBarFix] Rive .meta files check completed. Updated {fixedCount} files.");
            }
        }

        public static void FixActiveSceneRiveHUD(bool verbose)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var panels = Object.FindObjectsByType<RivePanel>(FindObjectsInactive.Include);
            bool sceneDirty = false;

            foreach (var panel in panels)
            {
                var canvasRenderer = panel.GetComponent<RiveCanvasRenderer>();
                if (canvasRenderer == null)
                {
                    Undo.AddComponent<RiveCanvasRenderer>(panel.gameObject);
                    sceneDirty = true;
                    Debug.Log($"[RiveHealthBarFix] Added RiveCanvasRenderer to '{panel.gameObject.name}' in scene '{activeScene.name}'.");
                }

                var canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = Undo.AddComponent<CanvasGroup>(panel.gameObject);
                    canvasGroup.alpha = 1f;
                    sceneDirty = true;
                    Debug.Log($"[RiveHealthBarFix] Added CanvasGroup to '{panel.gameObject.name}'.");
                }

                // Check widgets under this panel
                var widgets = panel.GetComponentsInChildren<RiveWidget>(true);
                foreach (var widget in widgets)
                {
                    bool isSpecialButton = widget.name.Contains("Special") || widget.name.Contains("Button") || widget.name.Contains("Bomb");
                    bool isHealthHud = widget.name.Contains("Health") || widget.name.Contains("alienwarhud") || widget.name.Equals("RiveWidget");

                    if (isSpecialButton)
                    {
                        // Remove rogue RiveHealthHUD if incorrectly added
                        var rogueHud = widget.GetComponent<RiveHealthHUD>();
                        if (rogueHud != null)
                        {
                            Undo.DestroyObjectImmediate(rogueHud);
                            sceneDirty = true;
                        }

                        var btn = widget.GetComponent<RiveSpecialButton>();
                        if (btn == null)
                        {
                            btn = Undo.AddComponent<RiveSpecialButton>(widget.gameObject);
                            btn.widget = widget;
                            sceneDirty = true;
                        }
                        else if (btn.widget == null)
                        {
                            btn.widget = widget;
                            EditorUtility.SetDirty(btn);
                            sceneDirty = true;
                        }

                        if (btn != null)
                        {
                            btn.ApplyLayout();
                            EditorUtility.SetDirty(widget.GetComponent<RectTransform>());
                            sceneDirty = true;
                        }
                    }
                    else if (isHealthHud)
                    {
                        var hud = widget.GetComponent<RiveHealthHUD>();
                        if (hud == null)
                        {
                            hud = Undo.AddComponent<RiveHealthHUD>(widget.gameObject);
                            hud.widget = widget;
                            sceneDirty = true;
                            Debug.Log($"[RiveHealthBarFix] Added RiveHealthHUD to '{widget.gameObject.name}'.");
                        }
                        else if (hud != null && hud.widget == null)
                        {
                            hud.widget = widget;
                            EditorUtility.SetDirty(hud);
                            sceneDirty = true;
                        }

                        if (hud != null)
                        {
                            hud.ApplyLayout();
                            EditorUtility.SetDirty(widget.GetComponent<RectTransform>());
                            sceneDirty = true;
                        }
                    }
                }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[RiveHealthBarFix] Scene '{activeScene.name}' saved with Rive Health Bar fixes.");
            }
            else if (verbose)
            {
                Debug.Log($"[RiveHealthBarFix] Scene '{activeScene.name}' Rive setup verified (all components already intact).");
            }
        }
    }
}
