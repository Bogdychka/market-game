using System;
using System.Collections.Generic;
using McpUnity.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Market.DebugTools.Editor
{
    /// <summary>Exposes the local Project Health report to the compact verification gate.</summary>
    [McpProjectTool]
    public sealed class ProjectHealthMcpTool : McpToolBase
    {
        public ProjectHealthMcpTool()
        {
            Name = "project_health_scan";
            Description = "Runs the Market project-owned health checks and returns structured findings.";
        }

        public override JObject Execute(JObject parameters)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Error("Exit Play Mode before running project verification.");

            var categories = new HashSet<ProjectHealthCategory>(
                (ProjectHealthCategory[])Enum.GetValues(typeof(ProjectHealthCategory)));
            ProjectHealthReport report = new ProjectHealthScanner().Scan(categories);
            return BuildResponse(report);
        }

        private static JObject BuildResponse(ProjectHealthReport report)
        {
            var issues = new JArray();
            foreach (ProjectHealthIssue issue in report.Issues)
            {
                issues.Add(new JObject
                {
                    ["severity"] = issue.Severity.ToString(),
                    ["category"] = issue.Category.ToString(),
                    ["title"] = issue.Title,
                    ["description"] = issue.Description,
                    ["assetPath"] = issue.AssetPath,
                    ["line"] = issue.Line
                });
            }

            return new JObject
            {
                ["success"] = true,
                ["status"] = report.Status.ToString().ToUpperInvariant(),
                ["errors"] = report.ErrorCount,
                ["warnings"] = report.WarningCount,
                ["info"] = report.InfoCount,
                ["checkedAssets"] = report.CheckedAssets,
                ["issues"] = issues
            };
        }

        private static JObject Error(string message)
        {
            return new JObject
            {
                ["success"] = false,
                ["type"] = "text",
                ["message"] = message
            };
        }
    }
}
