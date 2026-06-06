using System;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for executing Unity Editor menu items
    /// </summary>
    public class MenuItemTool : McpToolBase
    {
        private static bool _hasPendingPlayModeChange;
        private static bool _pendingPlayModeTargetState;

        public MenuItemTool()
        {
            Name = "execute_menu_item";
            Description = "Executes functions tagged with the MenuItem attribute";
        }
        
        /// <summary>
        /// Execute the MenuItem tool with the provided parameters synchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters with defaults
            string menuPath = parameters["menuPath"]?.ToObject<string>();
            if (string.IsNullOrEmpty(menuPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'menuPath' not provided", 
                    "validation_error"
                );
            }
                
            // Log the execution
            McpLogger.LogInfo($"[MCP Unity] Executing menu item: {menuPath}");

            if (IsPlayModeMenuItem(menuPath))
            {
                return TogglePlayMode(menuPath);
            }

            if (IsPauseMenuItem(menuPath))
            {
                return TogglePause(menuPath);
            }

            // Execute the menu item
            bool success = EditorApplication.ExecuteMenuItem(menuPath);
                
            // Create the response
            return new JObject
            {
                ["success"] = success,
                ["type"] = "text",
                ["message"] = success 
                    ? $"Successfully executed menu item: {menuPath}" 
                    : $"Failed to execute menu item: {menuPath}"
            };
        }

        private static bool IsPlayModeMenuItem(string menuPath)
        {
            return string.Equals(menuPath.Trim(), "Edit/Play", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPauseMenuItem(string menuPath)
        {
            return string.Equals(menuPath.Trim(), "Edit/Pause", StringComparison.OrdinalIgnoreCase);
        }

        private static JObject TogglePlayMode(string menuPath)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Cannot toggle Play Mode while Unity is compiling or updating",
                    "editor_busy_error"
                );
            }

            bool targetState = !EditorApplication.isPlaying;
            SchedulePlayModeChange(targetState);

            JObject response = CreateEditorStateResponse(
                true,
                menuPath,
                targetState ? "Scheduled enter Play Mode" : "Scheduled exit Play Mode"
            );
            response["scheduled"] = true;
            response["targetIsPlaying"] = targetState;
            return response;
        }

        private static void SchedulePlayModeChange(bool targetState)
        {
            _pendingPlayModeTargetState = targetState;

            if (_hasPendingPlayModeChange)
                return;

            _hasPendingPlayModeChange = true;
            EditorApplication.update -= ApplyPendingPlayModeChange;
            EditorApplication.update += ApplyPendingPlayModeChange;
        }

        private static void ApplyPendingPlayModeChange()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (IsPlayModeChangeAlreadySatisfied(_pendingPlayModeTargetState))
            {
                ClearPendingPlayModeChange();
                return;
            }

            bool targetState = _pendingPlayModeTargetState;
            ClearPendingPlayModeChange();
            EditorApplication.isPlaying = targetState;
        }

        private static bool IsPlayModeChangeAlreadySatisfied(bool targetState)
        {
            if (targetState)
            {
                return EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode;
            }

            return !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void ClearPendingPlayModeChange()
        {
            EditorApplication.update -= ApplyPendingPlayModeChange;
            _hasPendingPlayModeChange = false;
        }

        private static JObject TogglePause(string menuPath)
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Cannot toggle Pause because Unity is not in Play Mode",
                    "editor_state_error"
                );
            }

            EditorApplication.isPaused = !EditorApplication.isPaused;

            return CreateEditorStateResponse(
                true,
                menuPath,
                EditorApplication.isPaused ? "Requested pause Play Mode" : "Requested resume Play Mode"
            );
        }

        private static JObject CreateEditorStateResponse(bool success, string menuPath, string message)
        {
            return new JObject
            {
                ["success"] = success,
                ["type"] = "text",
                ["message"] = $"{message}: {menuPath}",
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPlayingOrWillChangePlaymode"] = EditorApplication.isPlayingOrWillChangePlaymode,
                ["isPaused"] = EditorApplication.isPaused
            };
        }
    }
}
