using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Starfall.Editor
{
    [InitializeOnLoad]
    public static class RivePackageSetup
    {
        // Previously forced a package resolve on every reload, which itself triggered another reload.
        // Use STARFALL > Rive > Resolve Rive Package only if the Rive package is missing.
        static RivePackageSetup() { }

        [MenuItem("STARFALL/Rive/Resolve Rive Package", priority = 50)]
        public static void CheckAndResolveRive()
        {
            Debug.Log("[RivePackageSetup] Triggering PackageManager resolve for Rive...");
            Client.Resolve();
        }
    }
}
