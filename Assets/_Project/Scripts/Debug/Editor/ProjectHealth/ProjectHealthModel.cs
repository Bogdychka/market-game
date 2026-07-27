using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Market.DebugTools.Editor
{
    public enum ProjectHealthSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum ProjectHealthCategory
    {
        Prefabs,
        ScriptableObjects,
        ProjectRules,
        Performance
    }

    public enum ProjectHealthStatus
    {
        Green,
        Yellow,
        Red
    }

    /// <summary>One actionable finding produced by a project health check.</summary>
    public sealed class ProjectHealthIssue
    {
        public ProjectHealthIssue(
            ProjectHealthSeverity severity,
            ProjectHealthCategory category,
            string title,
            string description,
            string assetPath,
            int line = 0)
        {
            Severity = severity;
            Category = category;
            Title = title;
            Description = description;
            AssetPath = assetPath;
            Line = line;
        }

        public ProjectHealthSeverity Severity { get; }
        public ProjectHealthCategory Category { get; }
        public string Title { get; }
        public string Description { get; }
        public string AssetPath { get; }
        public int Line { get; }
    }

    /// <summary>Aggregates scanner findings and derives the overall status.</summary>
    public sealed class ProjectHealthReport
    {
        private readonly List<ProjectHealthIssue> _issues = new();

        public IReadOnlyList<ProjectHealthIssue> Issues => _issues;
        public int CheckedAssets { get; internal set; }
        public int ErrorCount => Count(ProjectHealthSeverity.Error);
        public int WarningCount => Count(ProjectHealthSeverity.Warning);
        public int InfoCount => Count(ProjectHealthSeverity.Info);
        public ProjectHealthStatus Status => ErrorCount > 0
            ? ProjectHealthStatus.Red
            : WarningCount > 0 ? ProjectHealthStatus.Yellow : ProjectHealthStatus.Green;

        public void Add(ProjectHealthIssue issue)
        {
            if (issue == null)
                throw new ArgumentNullException(nameof(issue));

            _issues.Add(issue);
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("MARKET PROJECT HEALTH");
            builder.AppendLine();
            builder.AppendLine($"Status: {Status.ToString().ToUpperInvariant()}");
            builder.AppendLine($"Errors: {ErrorCount}");
            builder.AppendLine($"Warnings: {WarningCount}");
            builder.AppendLine($"Info: {InfoCount}");
            builder.AppendLine($"Checked assets: {CheckedAssets}");

            foreach (ProjectHealthIssue issue in _issues)
            {
                builder.AppendLine();
                builder.AppendLine($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.Title}");
                builder.AppendLine(issue.Line > 0 ? $"{issue.AssetPath}:{issue.Line}" : issue.AssetPath);
                builder.AppendLine(issue.Description);
            }

            return builder.ToString();
        }

        private int Count(ProjectHealthSeverity severity)
        {
            return _issues.Count(issue => issue.Severity == severity);
        }
    }
}
