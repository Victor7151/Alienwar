using System.IO;
using UnityEditor;
using UnityEngine;

namespace Starfall.Editor
{
    /// <summary>
    /// Makes sure the gameplay .riv files in Assets/Resources/Rive are imported by the Rive importer
    /// (they were originally imported as plain files, which is why Rive never rendered).
    /// Runs once after each script reload and is available from the STARFALL menu.
    /// </summary>
    [InitializeOnLoad]
    public static class RiveAssetSetup
    {
        private const string RiveFolder = "Assets/Resources/Rive";

        static RiveAssetSetup()
        {
            // Once per editor session is enough; this only re-imports files that are not Rive assets yet.
            if (SessionState.GetBool("Starfall_RiveVerify_Ran", false)) return;
            SessionState.SetBool("Starfall_RiveVerify_Ran", true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) VerifyRiveAssets(false);
            };
        }

        [MenuItem("STARFALL/Rive/Import & Verify Rive Assets", priority = 49)]
        public static void VerifyFromMenu() => VerifyRiveAssets(true);

        public static void VerifyRiveAssets(bool verbose)
        {
            if (!AssetDatabase.IsValidFolder(RiveFolder)) return;

            int ok = 0, fixedCount = 0, failed = 0;
            foreach (string file in Directory.GetFiles(RiveFolder, "*.riv"))
            {
                string path = file.Replace('\\', '/');
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is global::Rive.Asset)
                {
                    ok++;
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is global::Rive.Asset)
                {
                    fixedCount++;
                    Debug.Log($"[RiveAssetSetup] Re-imported {path} with the Rive importer.");
                }
                else
                {
                    failed++;
                    Debug.LogWarning($"[RiveAssetSetup] {path} is still not a Rive asset (got {(main != null ? main.GetType().Name : "null")}). " +
                                     "Check that the Rive package (app.rive.rive-unity) resolved without errors in Package Manager.");
                }
            }

            if (verbose || fixedCount > 0 || failed > 0)
                Debug.Log($"[RiveAssetSetup] Rive assets — ok: {ok}, re-imported: {fixedCount}, failed: {failed}.");
        }
    }
}
