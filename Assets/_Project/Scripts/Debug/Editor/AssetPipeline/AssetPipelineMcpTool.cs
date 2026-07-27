using System;
using McpUnity.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>Provides structured, read-only model analysis to the compact MCP facade.</summary>
    [McpProjectTool]
    public sealed class AssetPipelineMcpTool : McpToolBase
    {
        public AssetPipelineMcpTool()
        {
            Name = "asset_pipeline_analyze";
            Description = "Analyzes one imported FBX or OBJ model without changing it.";
        }

        public override JObject Execute(JObject parameters)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Error("Exit Play Mode before analyzing an imported model.");

            string assetPath = parameters?.Value<string>("assetPath");
            string profileName = parameters?.Value<string>("profile") ?? nameof(AssetPipelineProfileId.StaticProp);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return Error("assetPath must point to an imported model under Assets/.");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (!AssetPipelineAnalyzer.IsModelAsset(model))
                return Error($"'{assetPath}' is not an imported FBX or OBJ model.");
            if (!Enum.TryParse(profileName, true, out AssetPipelineProfileId profile))
                return Error($"Unknown asset pipeline profile '{profileName}'.");

            AssetPipelineReport report = new AssetPipelineAnalyzer().Analyze(model, profile);
            return BuildResponse(report);
        }

        private static JObject BuildResponse(AssetPipelineReport report)
        {
            var issues = new JArray();
            foreach (AssetPipelineIssue issue in report.Issues)
            {
                issues.Add(new JObject
                {
                    ["severity"] = issue.Severity.ToString(),
                    ["title"] = issue.Title,
                    ["description"] = issue.Description
                });
            }

            return new JObject
            {
                ["success"] = true,
                ["status"] = report.Status.ToString().ToUpperInvariant(),
                ["assetPath"] = report.AssetPath,
                ["profile"] = report.Profile.ToString(),
                ["dimensions"] = new JObject
                {
                    ["x"] = report.Dimensions.x,
                    ["y"] = report.Dimensions.y,
                    ["z"] = report.Dimensions.z
                },
                ["meshCount"] = report.MeshCount,
                ["vertexCount"] = report.VertexCount,
                ["triangleCount"] = report.TriangleCount,
                ["materialCount"] = report.MaterialCount,
                ["hasCollider"] = report.HasCollider,
                ["hasProjectPrefab"] = report.HasProjectPrefab,
                ["importScale"] = report.ImportScale,
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
