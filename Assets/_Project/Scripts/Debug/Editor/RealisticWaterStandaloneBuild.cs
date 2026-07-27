using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Produces the isolated R9 Windows development build used by the promotion gate.
    /// </summary>
    public static class RealisticWaterStandaloneBuild
    {
        private const string ScenePath = "Assets/_Project/Scenes/WaterShaderLab.unity";

        /// <summary>
        /// Builds WaterShaderLab alone as a Windows development player.
        /// </summary>
        [MenuItem("Market/Debug/Water/Build R9 Standalone Development")]
        public static void Build()
        {
            string outputFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Artifacts",
                "RealisticWater",
                "R9",
                "StandaloneBuild");
            string executablePath = Path.Combine(outputFolder, "WaterShaderLab.exe");
            try
            {
                Directory.CreateDirectory(outputFolder);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                WriteBuildReport(outputFolder, executablePath, report.summary);
                LogResult(report.summary, executablePath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneBuild: build failed: {exception.Message}");
            }
        }

        private static void WriteBuildReport(
            string outputFolder,
            string executablePath,
            BuildSummary summary)
        {
            try
            {
                var report = new StringBuilder(1024);
                report.AppendLine("# R9 Standalone Development Build");
                report.AppendLine();
                report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                report.AppendLine($"Unity: {Application.unityVersion}");
                report.AppendLine($"Scene: `{ScenePath}`");
                report.AppendLine("Target: StandaloneWindows64");
                report.AppendLine("Options: Development, AllowDebugging");
                report.AppendLine($"Result: {summary.result}");
                report.AppendLine($"Errors: {summary.totalErrors}");
                report.AppendLine($"Warnings: {summary.totalWarnings}");
                report.AppendLine($"Size: {summary.totalSize} bytes");
                report.AppendLine($"Duration: {summary.totalTime}");
                report.AppendLine($"Executable: `{executablePath}`");
                File.WriteAllText(
                    Path.Combine(outputFolder, "build_report.md"),
                    report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneBuild: report failed: {exception.Message}");
            }
        }

        private static void LogResult(BuildSummary summary, string executablePath)
        {
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"RealisticWaterStandaloneBuild: succeeded at {executablePath}.");
            }
            else
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneBuild: result {summary.result}, " +
                    $"errors {summary.totalErrors}, warnings {summary.totalWarnings}.");
            }
        }
    }
}
