using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpUnity.Services;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting information about the active Unity scene
    /// </summary>
    public class GetSceneInfoTool : McpToolBase
    {
        public GetSceneInfoTool()
        {
            Name = "get_scene_info";
            Description = "Gets information about the active scene including name, path, dirty state, root object count, and loaded state";
        }

        /// <summary>
        /// Execute the GetSceneInfo tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();

                if (!activeScene.IsValid())
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "No valid active scene",
                        "validation_error"
                    );
                }

                // Get all loaded scenes info
                int loadedSceneCount = SceneManager.sceneCount;
                var loadedScenes = new JArray();

                for (int i = 0; i < loadedSceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    loadedScenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["buildIndex"] = scene.buildIndex,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                        ["rootCount"] = scene.isLoaded ? scene.rootCount : 0,
                        ["isActive"] = scene == activeScene
                    });
                }

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Active scene: '{activeScene.name}'",
                    ["activeScene"] = new JObject
                    {
                        ["name"] = activeScene.name,
                        ["path"] = activeScene.path,
                        ["buildIndex"] = activeScene.buildIndex,
                        ["isDirty"] = activeScene.isDirty,
                        ["isLoaded"] = activeScene.isLoaded,
                        ["rootCount"] = activeScene.isLoaded ? activeScene.rootCount : 0
                    },
                    ["loadedSceneCount"] = loadedSceneCount,
                    ["loadedScenes"] = loadedScenes
                };

                McpLogger.LogInfo($"Retrieved scene info for active scene '{activeScene.name}'");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting scene info: {ex.Message}",
                    "scene_info_error"
                );
            }
        }
    }

    /// <summary>
    /// Tool for collecting a compact Unity Editor health report in one request.
    /// </summary>
    public class GetHealthReportTool : McpToolBase
    {
        private const int DefaultMaxConsoleErrors = 20;
        private const int DefaultMaxTests = 50;
        private const int MaxConsoleErrorsLimit = 200;
        private const int MaxTestsLimit = 500;

        private readonly IConsoleLogsService _consoleLogsService;
        private readonly ITestRunnerService _testRunnerService;

        public GetHealthReportTool(IConsoleLogsService consoleLogsService, ITestRunnerService testRunnerService)
        {
            Name = "get_health_report";
            Description = "Collects a compact Unity health report: scenes, compile state, console errors, dirty scenes, tests, and build settings";
            IsAsync = true;
            _consoleLogsService = consoleLogsService;
            _testRunnerService = testRunnerService;
        }

        /// <summary>
        /// Executes the health report tool asynchronously.
        /// </summary>
        public override async void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                bool includeTests = GetBoolParameter(parameters, "includeTests", true);
                string testMode = parameters?["testMode"]?.ToObject<string>() ?? string.Empty;
                int maxConsoleErrors = Mathf.Clamp(
                    GetIntParameter(parameters, "maxConsoleErrors", DefaultMaxConsoleErrors),
                    0,
                    MaxConsoleErrorsLimit);
                int maxTests = Mathf.Clamp(
                    GetIntParameter(parameters, "maxTests", DefaultMaxTests),
                    0,
                    MaxTestsLimit);

                JObject sceneInfo = BuildSceneInfo();
                JObject compileState = BuildCompileState();
                JObject consoleErrors = BuildConsoleErrors(maxConsoleErrors);
                JObject buildSettings = BuildBuildSettings();
                JObject tests = includeTests
                    ? await BuildTestsInfoAsync(testMode, maxTests)
                    : BuildSkippedTestsInfo();

                string overallStatus = ResolveOverallStatus(sceneInfo, compileState, consoleErrors);
                string message = BuildSummaryMessage(overallStatus, sceneInfo, compileState, consoleErrors, tests);

                tcs.SetResult(new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = message,
                    ["overallStatus"] = overallStatus,
                    ["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    ["scene"] = sceneInfo,
                    ["compileState"] = compileState,
                    ["consoleErrors"] = consoleErrors,
                    ["dirtyScenes"] = sceneInfo["dirtyScenes"],
                    ["tests"] = tests,
                    ["buildSettings"] = buildSettings
                });
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"GetHealthReportTool failed: {ex.Message}");
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to collect health report: {ex.Message}",
                    "health_report_error"));
            }
        }

        private static JObject BuildSceneInfo()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            JArray loadedScenes = new JArray();
            JArray dirtyScenes = new JArray();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                JObject sceneObject = new JObject
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["buildIndex"] = scene.buildIndex,
                    ["isLoaded"] = scene.isLoaded,
                    ["isDirty"] = scene.isDirty,
                    ["rootCount"] = scene.isLoaded ? scene.rootCount : 0,
                    ["isActive"] = scene == activeScene
                };

                loadedScenes.Add(sceneObject);

                if (scene.isDirty)
                {
                    dirtyScenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["isActive"] = scene == activeScene
                    });
                }
            }

            return new JObject
            {
                ["activeScene"] = activeScene.IsValid()
                    ? new JObject
                    {
                        ["name"] = activeScene.name,
                        ["path"] = activeScene.path,
                        ["buildIndex"] = activeScene.buildIndex,
                        ["isDirty"] = activeScene.isDirty,
                        ["isLoaded"] = activeScene.isLoaded,
                        ["rootCount"] = activeScene.isLoaded ? activeScene.rootCount : 0
                    }
                    : null,
                ["loadedSceneCount"] = SceneManager.sceneCount,
                ["loadedScenes"] = loadedScenes,
                ["dirtySceneCount"] = dirtyScenes.Count,
                ["dirtyScenes"] = dirtyScenes
            };
        }

        private static JObject BuildCompileState()
        {
            return new JObject
            {
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isUpdating"] = EditorApplication.isUpdating,
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["scriptCompilationFailed"] = EditorUtility.scriptCompilationFailed
            };
        }

        private JObject BuildConsoleErrors(int maxConsoleErrors)
        {
            JObject errors = _consoleLogsService.GetLogsAsJson("error", 0, maxConsoleErrors, false);
            JObject counts = GetConsoleCounts();

            return new JObject
            {
                ["errorCount"] = counts["errorCount"],
                ["warningCount"] = counts["warningCount"],
                ["logCount"] = counts["logCount"],
                ["capturedErrorCount"] = errors["_filteredCount"],
                ["returnedErrorCount"] = errors["_returnedCount"],
                ["maxReturned"] = maxConsoleErrors,
                ["errors"] = errors["logs"]
            };
        }

        private static JObject GetConsoleCounts()
        {
#if UNITY_6000_0_OR_NEWER
            ConsoleWindowUtility.GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
            return new JObject
            {
                ["errorCount"] = errorCount,
                ["warningCount"] = warningCount,
                ["logCount"] = logCount
            };
#else
            return new JObject
            {
                ["errorCount"] = 0,
                ["warningCount"] = 0,
                ["logCount"] = 0
            };
#endif
        }

        private static JObject BuildBuildSettings()
        {
            JArray scenes = new JArray();
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;

            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene scene = buildScenes[i];
                scenes.Add(new JObject
                {
                    ["index"] = i,
                    ["path"] = scene.path,
                    ["enabled"] = scene.enabled
                });
            }

            BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(activeBuildTargetGroup);

            return new JObject
            {
                ["activeBuildTarget"] = activeBuildTarget.ToString(),
                ["activeBuildTargetGroup"] = activeBuildTargetGroup.ToString(),
                ["selectedBuildTargetGroup"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                ["development"] = EditorUserBuildSettings.development,
                ["allowDebugging"] = EditorUserBuildSettings.allowDebugging,
                ["productName"] = PlayerSettings.productName,
                ["companyName"] = PlayerSettings.companyName,
                ["applicationIdentifier"] = PlayerSettings.GetApplicationIdentifier(namedBuildTarget),
                ["buildSceneCount"] = scenes.Count,
                ["enabledBuildSceneCount"] = buildScenes.Count(scene => scene.enabled),
                ["scenes"] = scenes
            };
        }

        private async Task<JObject> BuildTestsInfoAsync(string testMode, int maxTests)
        {
            JArray sample = new JArray();
            Dictionary<string, int> countsByMode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalCount = 0;

            if (string.IsNullOrEmpty(testMode))
            {
                totalCount += await AddTestsForModeAsync("EditMode", maxTests, sample, countsByMode);
                totalCount += await AddTestsForModeAsync("PlayMode", maxTests, sample, countsByMode);
            }
            else
            {
                totalCount += await AddTestsForModeAsync(testMode, maxTests, sample, countsByMode);
            }

            JObject byMode = new JObject();
            foreach (KeyValuePair<string, int> pair in countsByMode.OrderBy(pair => pair.Key))
            {
                byMode[pair.Key] = pair.Value;
            }

            return new JObject
            {
                ["included"] = true,
                ["testModeFilter"] = string.IsNullOrEmpty(testMode) ? null : testMode,
                ["availableCount"] = totalCount,
                ["returnedCount"] = sample.Count,
                ["maxReturned"] = maxTests,
                ["byMode"] = byMode,
                ["tests"] = sample
            };
        }

        private async Task<int> AddTestsForModeAsync(
            string mode,
            int maxTests,
            JArray sample,
            Dictionary<string, int> countsByMode)
        {
            List<ITestAdaptor> tests = await _testRunnerService.GetAllTestsAsync(mode);
            countsByMode.TryGetValue(mode, out int currentCount);
            countsByMode[mode] = currentCount + tests.Count;

            foreach (ITestAdaptor test in tests)
            {
                if (sample.Count >= maxTests)
                {
                    break;
                }

                sample.Add(new JObject
                {
                    ["name"] = test.Name,
                    ["fullName"] = test.FullName,
                    ["testMode"] = mode,
                    ["runState"] = test.RunState.ToString()
                });
            }

            return tests.Count;
        }

        private static JObject BuildSkippedTestsInfo()
        {
            return new JObject
            {
                ["included"] = false,
                ["message"] = "Test discovery skipped by includeTests=false"
            };
        }

        private static string ResolveOverallStatus(JObject sceneInfo, JObject compileState, JObject consoleErrors)
        {
            if (compileState.Value<bool>("scriptCompilationFailed") || consoleErrors.Value<int>("errorCount") > 0)
            {
                return "attention";
            }

            if (compileState.Value<bool>("isCompiling") || compileState.Value<bool>("isUpdating"))
            {
                return "compiling";
            }

            if (sceneInfo.Value<int>("dirtySceneCount") > 0)
            {
                return "dirty";
            }

            return "ok";
        }

        private static string BuildSummaryMessage(
            string status,
            JObject sceneInfo,
            JObject compileState,
            JObject consoleErrors,
            JObject tests)
        {
            string activeSceneName = sceneInfo["activeScene"]?["name"]?.ToString() ?? "(none)";
            int errorCount = consoleErrors.Value<int>("errorCount");
            int dirtySceneCount = sceneInfo.Value<int>("dirtySceneCount");
            int testCount = tests.Value<bool?>("included") == true ? tests.Value<int>("availableCount") : -1;
            string testSummary = testCount >= 0 ? $"{testCount} test(s)" : "tests skipped";

            return $"Health report: {status}. Scene='{activeSceneName}', compileFailed={compileState.Value<bool>("scriptCompilationFailed")}, consoleErrors={errorCount}, dirtyScenes={dirtySceneCount}, {testSummary}.";
        }

        private static int GetIntParameter(JObject parameters, string key, int defaultValue)
        {
            if (parameters?[key] != null && int.TryParse(parameters[key].ToString(), out int value))
            {
                return value;
            }

            return defaultValue;
        }

        private static bool GetBoolParameter(JObject parameters, string key, bool defaultValue)
        {
            if (parameters?[key] != null && bool.TryParse(parameters[key].ToString(), out bool value))
            {
                return value;
            }

            return defaultValue;
        }
    }
}
