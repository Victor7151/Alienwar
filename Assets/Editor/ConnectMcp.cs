using UnityEditor;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;

namespace Starfall.Editor
{
    [InitializeOnLoad]
    public static class ConnectMcp
    {
        static ConnectMcp()
        {
            EditorApplication.delayCall += () =>
            {
                UnityEngine.Debug.Log("[ConnectMcp] Attempting to connect HTTP transport...");
                MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            };
        }
    }
}
