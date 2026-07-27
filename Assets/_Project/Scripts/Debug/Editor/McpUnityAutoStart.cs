#if UNITY_EDITOR
using System;
using McpUnity.Unity;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Starts the local MCP Unity bridge when the package auto-start is delayed after a domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpUnityAutoStart
    {
        private const double StartupDelaySeconds = 2.0;

        private static readonly double StartAt;

        static McpUnityAutoStart()
        {
            StartAt = EditorApplication.timeSinceStartup + StartupDelaySeconds;
            EditorApplication.update -= TryStartServer;
            EditorApplication.update += TryStartServer;
        }

        private static void TryStartServer()
        {
            if (EditorApplication.timeSinceStartup < StartAt)
            {
                return;
            }

            EditorApplication.update -= TryStartServer;
            if (!McpUnitySettings.Instance.AutoStartServer)
            {
                return;
            }

            try
            {
                McpUnityServer server = McpUnityServer.Instance;
                if (server == null || server.IsListening)
                {
                    return;
                }

                server.StartServer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity AutoStart] Failed to start MCP Unity server: {ex.Message}");
            }
        }
    }
}
#endif
