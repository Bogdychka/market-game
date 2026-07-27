using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Tools
{
    /// <summary>
    /// Drives a compatible first-person controller in Play Mode and captures the resulting Game View.
    /// Project gameplay types are resolved by reflection so the MCP package stays project-independent.
    /// </summary>
    public sealed class PlayerAgentTool : McpToolBase
    {
        private const string PlayerTypeName = "Market.Player.FirstPersonController";
        private const string InteractionTypeName = "Market.Interaction.InteractionSystem";
        private const string AgentMethodName = "RunAgentAction";
        private const string InteractMethodName = "TryInteract";
        private const float MaxDuration = 3f;
        private const double CaptureTimeoutSeconds = 3d;

        public PlayerAgentTool()
        {
            Name = "player_agent";
            Description = "Drives the live first-person player in Play Mode and returns a Game View observation.";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(ExecuteCoroutine(parameters, tcs));
        }

        private static IEnumerator ExecuteCoroutine(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                tcs.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "player_agent requires active, unpaused Play Mode.",
                    "invalid_state"));
                yield break;
            }

            Component player = FindSceneComponent(PlayerTypeName);
            if (player == null)
            {
                tcs.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"No active {PlayerTypeName} component was found.",
                    "not_found"));
                yield break;
            }

            JObject actionError = ExecuteAction(player, parameters);
            if (actionError != null)
            {
                tcs.TrySetResult(actionError);
                yield break;
            }

            yield return null;
            yield return null;

            if (parameters.Value<bool?>("interact") == true)
            {
                TryInteract(player);
                yield return null;
            }

            string imagePath = PrepareCapturePath();
            if (string.IsNullOrEmpty(imagePath))
            {
                tcs.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Could not prepare the player-agent capture path.",
                    "capture_error"));
                yield break;
            }

            if (!TryCaptureCameraView(player, imagePath))
            {
                ScreenCapture.CaptureScreenshot(imagePath);
                double deadline = EditorApplication.timeSinceStartup + CaptureTimeoutSeconds;
                while (!HasCapture(imagePath) && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
            }

            if (!HasCapture(imagePath))
            {
                tcs.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Game View screenshot was not written before the capture timeout.",
                    "capture_timeout"));
                yield break;
            }

            tcs.TrySetResult(BuildObservation(player, imagePath));
        }

        private static JObject ExecuteAction(Component player, JObject parameters)
        {
            try
            {
                float moveX = Mathf.Clamp(parameters.Value<float?>("moveX") ?? 0f, -1f, 1f);
                float moveY = Mathf.Clamp(parameters.Value<float?>("moveY") ?? 0f, -1f, 1f);
                float yaw = Mathf.Clamp(parameters.Value<float?>("yawDegrees") ?? 0f, -180f, 180f);
                float pitch = Mathf.Clamp(parameters.Value<float?>("pitchDegrees") ?? 0f, -85f, 85f);
                float duration = Mathf.Clamp(parameters.Value<float?>("duration") ?? 0f, 0f, MaxDuration);
                bool sprint = parameters.Value<bool?>("sprint") ?? false;
                bool jump = parameters.Value<bool?>("jump") ?? false;

                MethodInfo actionMethod = player.GetType().GetMethod(AgentMethodName, BindingFlags.Public | BindingFlags.Instance);
                if (actionMethod == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"{PlayerTypeName}.{AgentMethodName} is unavailable. Recompile the project in the Unity Editor.",
                        "not_supported");
                }

                actionMethod.Invoke(player, new object[]
                {
                    new Vector2(moveX, moveY), yaw, pitch, sprint, jump, duration
                });

                return null;
            }
            catch (Exception exception)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Player action failed: {exception.GetBaseException().Message}",
                    "action_error");
            }
        }

        private static void TryInteract(Component player)
        {
            Component interaction = FindPlayerInteraction(player);
            MethodInfo method = interaction?.GetType().GetMethod(InteractMethodName, BindingFlags.Public | BindingFlags.Instance);
            method?.Invoke(interaction, null);
        }

        private static JObject BuildObservation(Component player, string imagePath)
        {
            Camera camera = player.GetComponentInChildren<Camera>(true);
            Component interaction = FindPlayerInteraction(player);
            object current = interaction?.GetType()
                .GetProperty("Current", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(interaction);
            string prompt = current?.GetType()
                .GetProperty("PromptText", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(current) as string;

            Vector3 position = player.transform.position;
            Vector3 rotation = camera != null ? camera.transform.eulerAngles : player.transform.eulerAngles;
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = "Player action completed and Game View captured.",
                ["imagePath"] = imagePath,
                ["scene"] = player.gameObject.scene.name,
                ["playerPath"] = GetHierarchyPath(player.transform),
                ["position"] = VectorToJson(position),
                ["viewEuler"] = VectorToJson(rotation),
                ["grounded"] = player.GetComponent<CharacterController>()?.isGrounded ?? false,
                ["interactionPrompt"] = prompt ?? string.Empty,
                ["interactionType"] = current?.GetType().FullName ?? string.Empty
            };
        }

        private static Component FindPlayerInteraction(Component player)
        {
            Type interactionType = ResolveType(InteractionTypeName);
            return interactionType == null ? null : player.GetComponentInChildren(interactionType, true);
        }

        private static Component FindSceneComponent(string fullTypeName)
        {
            Type type = ResolveType(fullTypeName);
            if (type == null)
                return null;

            UnityEngine.Object[] candidates = UnityEngine.Resources.FindObjectsOfTypeAll(type);
            foreach (UnityEngine.Object candidate in candidates)
            {
                if (candidate is Component component &&
                    component.gameObject.scene.IsValid() &&
                    component.gameObject.activeInHierarchy)
                {
                    return component;
                }
            }

            return null;
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static string PrepareCapturePath()
        {
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PlayerAgent"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "latest.png");
                if (File.Exists(path))
                    File.Delete(path);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerAgentTool] Capture path error: {exception.Message}");
                return null;
            }
        }

        private static bool HasCapture(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 8;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerAgentTool] Capture check error: {exception.Message}");
                return false;
            }
        }

        private static bool TryCaptureCameraView(Component player, string path)
        {
            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null)
                return false;

            const int Width = 960;
            const int Height = 540;
            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D image = null;
            List<CanvasState> canvasStates = ConvertOverlayCanvases(camera);

            try
            {
                renderTexture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
                return HasCapture(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerAgentTool] Direct camera capture failed: {exception.Message}");
                return false;
            }
            finally
            {
                RestoreCanvases(canvasStates);
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static List<CanvasState> ConvertOverlayCanvases(Camera camera)
        {
            Canvas[] canvases = UnityEngine.Resources.FindObjectsOfTypeAll<Canvas>();
            var states = new List<CanvasState>(canvases.Length);
            foreach (Canvas canvas in canvases)
            {
                if (!canvas.gameObject.scene.IsValid() ||
                    !canvas.gameObject.activeInHierarchy ||
                    canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                states.Add(new CanvasState(canvas));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 0.5f);
            }

            return states;
        }

        private static void RestoreCanvases(List<CanvasState> states)
        {
            foreach (CanvasState state in states)
                state.Restore();
        }

        private static JObject VectorToJson(Vector3 value)
        {
            return new JObject
            {
                ["x"] = value.x,
                ["y"] = value.y,
                ["z"] = value.z
            };
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private readonly struct CanvasState
        {
            private readonly Canvas _canvas;
            private readonly RenderMode _renderMode;
            private readonly Camera _worldCamera;
            private readonly float _planeDistance;

            public CanvasState(Canvas canvas)
            {
                _canvas = canvas;
                _renderMode = canvas.renderMode;
                _worldCamera = canvas.worldCamera;
                _planeDistance = canvas.planeDistance;
            }

            public void Restore()
            {
                if (_canvas == null)
                    return;

                _canvas.renderMode = _renderMode;
                _canvas.worldCamera = _worldCamera;
                _canvas.planeDistance = _planeDistance;
            }
        }
    }
}
