#if UNITY_EDITOR
using System;
using McpUnity.Unity;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Starts the local MCP Unity bridge after a domain reload.
    ///
    /// This matters most when entering Play mode: the package closes every client with code 4001
    /// and the play-mode domain reload drops the server instance, and its own restart hooks
    /// (<c>DidReloadScripts</c>, <c>afterAssemblyReload</c>) only cover compilation reloads. Until
    /// something touches <c>McpUnityServer.Instance</c> again the bridge stays down for the whole
    /// play session, so this keeps retrying until the socket is actually listening instead of
    /// firing once after a fixed wait.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpUnityAutoStart
    {
        // Long enough for the reload to settle, short enough that a capture or health check issued
        // right after Play mode does not sit in a dead window.
        private const double FirstAttemptDelaySeconds = 0.4;
        private const double RetryIntervalSeconds = 0.5;
        private const double GiveUpAfterSeconds = 20.0;

        private static readonly double FirstAttemptAt;
        private static readonly double GiveUpAt;
        private static double _nextAttemptAt;

        static McpUnityAutoStart()
        {
            FirstAttemptAt = EditorApplication.timeSinceStartup + FirstAttemptDelaySeconds;
            GiveUpAt = FirstAttemptAt + GiveUpAfterSeconds;
            _nextAttemptAt = FirstAttemptAt;
            EditorApplication.update -= TryStartServer;
            EditorApplication.update += TryStartServer;
        }

        private static void TryStartServer()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextAttemptAt)
            {
                return;
            }

            if (!McpUnitySettings.Instance.AutoStartServer || now > GiveUpAt)
            {
                EditorApplication.update -= TryStartServer;
                return;
            }

            _nextAttemptAt = now + RetryIntervalSeconds;

            try
            {
                // Reading Instance is what re-creates the server after a play-mode domain reload.
                McpUnityServer server = McpUnityServer.Instance;
                if (server == null)
                {
                    return;
                }

                if (server.IsListening)
                {
                    EditorApplication.update -= TryStartServer;
                    return;
                }

                server.StartServer();
            }
            catch (Exception ex)
            {
                EditorApplication.update -= TryStartServer;
                Debug.LogError($"[MCP Unity AutoStart] Failed to start MCP Unity server: {ex.Message}");
            }
        }
    }
}
#endif
