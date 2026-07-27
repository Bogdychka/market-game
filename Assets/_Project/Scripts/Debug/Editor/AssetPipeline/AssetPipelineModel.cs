using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    public enum AssetPipelineProfileId
    {
        StaticProp,
        FoodItem,
        Structure,
        Character
    }

    public enum AssetPipelineSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum AssetPipelineStatus
    {
        Ready,
        Warning,
        Blocked
    }

    /// <summary>Broad, intentionally conservative limits for one imported model role.</summary>
    public readonly struct MarketAssetProfile
    {
        public MarketAssetProfile(
            AssetPipelineProfileId id,
            float minimumSize,
            float maximumSize,
            int triangleLimit,
            bool isStatic)
        {
            Id = id;
            MinimumSize = minimumSize;
            MaximumSize = maximumSize;
            TriangleLimit = triangleLimit;
            IsStatic = isStatic;
        }

        public AssetPipelineProfileId Id { get; }
        public float MinimumSize { get; }
        public float MaximumSize { get; }
        public int TriangleLimit { get; }
        public bool IsStatic { get; }

        public static MarketAssetProfile Get(AssetPipelineProfileId id)
        {
            return id switch
            {
                AssetPipelineProfileId.FoodItem => new(id, 0.02f, 0.8f, 10000, true),
                AssetPipelineProfileId.Structure => new(id, 1f, 50f, 200000, true),
                AssetPipelineProfileId.Character => new(id, 1f, 3f, 100000, false),
                _ => new(id, 0.05f, 5f, 50000, true)
            };
        }
    }

    /// <summary>One finding from a selected model analysis.</summary>
    public sealed class AssetPipelineIssue
    {
        public AssetPipelineIssue(AssetPipelineSeverity severity, string title, string description)
        {
            Severity = severity;
            Title = title;
            Description = description;
        }

        public AssetPipelineSeverity Severity { get; }
        public string Title { get; }
        public string Description { get; }
    }

    /// <summary>Cached metrics and findings for one imported model.</summary>
    public sealed class AssetPipelineReport
    {
        private readonly List<AssetPipelineIssue> _issues = new();

        public string AssetPath { get; internal set; }
        public AssetPipelineProfileId Profile { get; internal set; }
        public Vector3 Dimensions { get; internal set; }
        public int MeshCount { get; internal set; }
        public int VertexCount { get; internal set; }
        public long TriangleCount { get; internal set; }
        public int MaterialCount { get; internal set; }
        public bool HasCollider { get; internal set; }
        public bool HasProjectPrefab { get; internal set; }
        public float ImportScale { get; internal set; }
        public IReadOnlyList<AssetPipelineIssue> Issues => _issues;
        public int ErrorCount => Count(AssetPipelineSeverity.Error);
        public int WarningCount => Count(AssetPipelineSeverity.Warning);
        public AssetPipelineStatus Status => ErrorCount > 0
            ? AssetPipelineStatus.Blocked
            : WarningCount > 0 ? AssetPipelineStatus.Warning : AssetPipelineStatus.Ready;

        public void Add(AssetPipelineSeverity severity, string title, string description)
        {
            _issues.Add(new AssetPipelineIssue(severity, title, description));
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("MARKET ASSET PIPELINE REPORT");
            builder.AppendLine();
            builder.AppendLine($"Asset: {AssetPath}");
            builder.AppendLine($"Profile: {Profile}");
            builder.AppendLine($"Status: {Status.ToString().ToUpperInvariant()}");
            builder.AppendLine($"Dimensions: {Dimensions.x:0.###} x {Dimensions.y:0.###} x {Dimensions.z:0.###} m");
            builder.AppendLine($"Geometry: {VertexCount} vertices, {TriangleCount} triangles, {MeshCount} meshes");
            builder.AppendLine($"Materials: {MaterialCount}");
            builder.AppendLine($"Collider: {(HasCollider ? "yes" : "no")}");
            builder.AppendLine($"Project prefab: {(HasProjectPrefab ? "yes" : "no")}");

            foreach (AssetPipelineIssue issue in _issues)
            {
                builder.AppendLine();
                builder.AppendLine($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.Title}");
                builder.AppendLine(issue.Description);
            }

            return builder.ToString();
        }

        private int Count(AssetPipelineSeverity severity)
        {
            return _issues.Count(issue => issue.Severity == severity);
        }
    }

    /// <summary>Pure rules shared by the analyzer and EditMode tests.</summary>
    public static class AssetPipelineRules
    {
        public static bool IsGenericObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string name = value.Trim();
            return IsGenericPrefix(name, "Cube")
                || IsGenericPrefix(name, "Object")
                || IsGenericPrefix(name, "Mesh")
                || IsGenericPrefix(name, "Cylinder");
        }

        public static bool HasInvalidScale(Vector3 scale)
        {
            return scale.x <= 0f || scale.y <= 0f || scale.z <= 0f;
        }

        public static bool IsSuspiciousSize(float largestDimension, MarketAssetProfile profile)
        {
            return largestDimension <= 0f
                || largestDimension < profile.MinimumSize
                || largestDimension > profile.MaximumSize;
        }

        private static bool IsGenericPrefix(string value, string prefix)
        {
            return value.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
