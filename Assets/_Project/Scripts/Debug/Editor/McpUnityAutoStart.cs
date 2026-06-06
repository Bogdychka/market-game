#if UNITY_EDITOR
using System;
using McpUnity.Unity;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Fallback that starts the local MCP Unity bridge for Codex when the Unity Editor loads this project.
    /// The package already auto-starts via [InitializeOnLoad]/[DidReloadScripts] when
    /// <see cref="McpUnitySettings.AutoStartServer"/> is enabled; this delayed retry only covers the
    /// rare case where the package's own start was skipped. It respects the AutoStartServer setting so
    /// it never overrides a user who deliberately disabled auto-start.
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

            // Respect the user's choice: if auto-start is disabled in settings, do nothing.
            // Otherwise we would silently re-enable a server the user turned off.
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
