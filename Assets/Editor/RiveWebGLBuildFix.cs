#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Starfall.Editor
{
    /// <summary>
    /// Guarantees that the native Rive WebAssembly static libraries (.a)
    /// corresponding to the current Unity/Emscripten version are present
    /// and correctly configured for WebGL before building.
    /// </summary>
    public class RiveWebGLBuildFix : IPreprocessBuildWithReport
    {
        public const string TARGET_PLUGINS_PATH = "Assets/Plugins/WebGL/Rive";

        public int callbackOrder => -10; // Must run before Rive's internal preprocessor (order 0)

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                EnsureRiveLibraries();
            }
        }

        [MenuItem("STARFALL/Tools/Ensure Rive WebGL Plugins", priority = 40)]
        public static bool EnsureRiveLibraries()
        {
            try
            {
                string packageRoot = FindRivePackageRoot();
                if (string.IsNullOrEmpty(packageRoot))
                {
                    Debug.LogWarning("[RiveWebGLBuildFix] Could not locate Rive package directory.");
                    return false;
                }

                string emscriptenVersion = GetMatchingEmscriptenVersion(Application.unityVersion);
                string variantSuffix = "";
                
#if UNITY_6000_0_OR_NEWER
                bool useWasm2023 = PlayerSettings.WebGL.wasm2023 || PlayerSettings.WebGL.threadsSupport;
                bool useThreads = useWasm2023 && PlayerSettings.WebGL.threadsSupport;
                if (useWasm2023) variantSuffix += "_wasm2023";
                if (useThreads) variantSuffix += "_mt";
#endif

                string sourceDir = Path.Combine(packageRoot, "Runtime", "Libraries", "WebGL", $"emscripten_{emscriptenVersion}{variantSuffix}");
                if (!Directory.Exists(sourceDir))
                {
                    // Fallback to base emscripten version without suffix if variant not present
                    sourceDir = Path.Combine(packageRoot, "Runtime", "Libraries", "WebGL", $"emscripten_{emscriptenVersion}");
                }

                if (!Directory.Exists(sourceDir))
                {
                    Debug.LogError($"[RiveWebGLBuildFix] Rive WebGL native library directory not found: {sourceDir}");
                    return false;
                }

                if (!Directory.Exists("Assets/Plugins")) Directory.CreateDirectory("Assets/Plugins");
                if (!Directory.Exists("Assets/Plugins/WebGL")) Directory.CreateDirectory("Assets/Plugins/WebGL");
                if (!Directory.Exists(TARGET_PLUGINS_PATH)) Directory.CreateDirectory(TARGET_PLUGINS_PATH);

                string[] aFiles = Directory.GetFiles(sourceDir, "*.a");
                if (aFiles.Length == 0)
                {
                    Debug.LogWarning($"[RiveWebGLBuildFix] No .a library files found in {sourceDir}");
                    return false;
                }

                foreach (string file in aFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(TARGET_PLUGINS_PATH, fileName);
                    if (!File.Exists(destFile) || new FileInfo(destFile).Length != new FileInfo(file).Length)
                    {
                        File.Copy(file, destFile, true);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // Configure PluginImporter for every .a file to ensure WebGL compatibility
                foreach (string file in Directory.GetFiles(TARGET_PLUGINS_PATH, "*.a"))
                {
                    string assetPath = file.Replace('\\', '/');
                    var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
                    if (importer != null)
                    {
                        bool changed = false;
                        if (importer.GetCompatibleWithAnyPlatform())
                        {
                            importer.SetCompatibleWithAnyPlatform(false);
                            changed = true;
                        }

                        if (!importer.GetCompatibleWithPlatform(BuildTarget.WebGL))
                        {
                            importer.SetCompatibleWithPlatform(BuildTarget.WebGL, true);
                            changed = true;
                        }

                        // Ensure disabled for other targets
                        if (importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64))
                        {
                            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                            changed = true;
                        }
                        if (importer.GetCompatibleWithPlatform(BuildTarget.Android))
                        {
                            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                            changed = true;
                        }

                        if (changed)
                        {
                            importer.SaveAndReimport();
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[RiveWebGLBuildFix] Verified {aFiles.Length} Rive WebGL static libraries in {TARGET_PLUGINS_PATH} for Unity {Application.unityVersion} (Emscripten {emscriptenVersion}).</color>");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RiveWebGLBuildFix] Exception during library preparation: {ex}");
                return false;
            }
        }

        public static string FindRivePackageRoot()
        {
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/app.rive.rive-unity");
            if (pkgInfo != null && !string.IsNullOrEmpty(pkgInfo.resolvedPath) && Directory.Exists(pkgInfo.resolvedPath))
            {
                return pkgInfo.resolvedPath;
            }

            if (Directory.Exists("Packages/app.rive.rive-unity"))
            {
                return Path.GetFullPath("Packages/app.rive.rive-unity");
            }

            if (Directory.Exists("Library/PackageCache"))
            {
                var matches = Directory.GetDirectories("Library/PackageCache", "app.rive.rive-unity*");
                if (matches.Length > 0)
                {
                    return Path.GetFullPath(matches[0]);
                }
            }

            return null;
        }

        private static string GetMatchingEmscriptenVersion(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion)) return "4.0.19";

            string[] parts = unityVersion.Split('.');
            if (int.TryParse(parts[0], out int major))
            {
                if (major >= 6000)
                {
                    if (parts.Length > 1 && int.TryParse(parts[1], out int minor) && minor >= 5)
                    {
                        return "4.0.19";
                    }
                    return "4.0.19"; // Unity 6.5+ and 6000.6 use 4.0.19
                }
                else if (major >= 2023)
                {
                    return "3.1.38";
                }
            }

            return "3.1.8";
        }
    }
}
#endif
