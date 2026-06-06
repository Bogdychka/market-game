using System;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using McpUnity.Services;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for running Unity Test Runner tests
    /// </summary>
    public class RunTestsTool : McpToolBase
    {
        private readonly ITestRunnerService _testRunnerService;

        public RunTestsTool(ITestRunnerService testRunnerService)
        {
            Name = "run_tests";
            Description = "Runs tests using Unity's Test Runner";
            IsAsync = true;
            _testRunnerService = testRunnerService;
        }
        
        /// <summary>
        /// Executes the RunTests tool asynchronously on the main thread.
        /// </summary>
        /// <param name="parameters">Tool parameters, including optional 'testMode', 'testFilter', and 'timeoutSeconds'.</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception.</param>
        public override async void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                // Parse parameters
                string testModeStr = parameters?["testMode"]?.ToObject<string>() ?? "EditMode";
                string testFilter = parameters?["testFilter"]?.ToObject<string>(); // Optional
                bool returnOnlyFailures = parameters?["returnOnlyFailures"]?.ToObject<bool>() ?? false; // Optional
                bool returnWithLogs = parameters?["returnWithLogs"]?.ToObject<bool>() ?? false; // Optional

                if (!TryParseTimeoutSeconds(parameters, out int? timeoutSeconds, out string timeoutError))
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(timeoutError, "validation_error"));
                    return;
                }

                TestMode testMode = TestMode.EditMode;
                
                if (Enum.TryParse(testModeStr, true, out TestMode parsedMode))
                {
                    testMode = parsedMode;
                }

                McpLogger.LogInfo($"Executing RunTestsTool: Mode={testMode}, Filter={testFilter ?? "(none)"}, Timeout={timeoutSeconds?.ToString() ?? "global"}s");

                // Call the service to run tests
                JObject result = await _testRunnerService.ExecuteTestsAsync(testMode, returnOnlyFailures, returnWithLogs, testFilter, timeoutSeconds);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"RunTestsTool failed: {ex.Message}");
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to run tests: {ex.Message}",
                    "test_runner_error"));
            }
        }

        private static bool TryParseTimeoutSeconds(JObject parameters, out int? timeoutSeconds, out string errorMessage)
        {
            timeoutSeconds = null;
            errorMessage = null;

            JToken timeoutToken = parameters?["timeoutSeconds"];
            if (timeoutToken == null || timeoutToken.Type == JTokenType.Null)
            {
                return true;
            }

            double timeoutValue;
            try
            {
                timeoutValue = timeoutToken.ToObject<double>();
            }
            catch (Exception)
            {
                errorMessage = "timeoutSeconds must be a positive number.";
                return false;
            }

            if (double.IsNaN(timeoutValue) || double.IsInfinity(timeoutValue) || timeoutValue <= 0)
            {
                errorMessage = "timeoutSeconds must be greater than zero.";
                return false;
            }

            if (timeoutValue > int.MaxValue)
            {
                errorMessage = $"timeoutSeconds must be less than or equal to {int.MaxValue}.";
                return false;
            }

            timeoutSeconds = (int)Math.Ceiling(timeoutValue);
            return true;
        }
    }
}
