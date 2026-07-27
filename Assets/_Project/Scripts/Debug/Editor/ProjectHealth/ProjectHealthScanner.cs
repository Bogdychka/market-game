using System;
using System.Collections.Generic;
using Market.DebugTools.Editor.Checks;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>Runs selected read-only health checks without changing scenes or assets.</summary>
    public sealed class ProjectHealthScanner
    {
        private readonly IProjectHealthCheck[] _checks =
        {
            new PrefabMissingScriptCheck(),
            new ItemDataCheck(),
            new CropDataCheck(),
            new NpcTypeDataCheck(),
            new OutdoorScenePerformanceCheck(),
            new AsciiContentCheck()
        };

        [MenuItem("Market Game/Run Project Health Scan")]
        public static void RunAllFromMenu()
        {
            var categories = new HashSet<ProjectHealthCategory>(
                (ProjectHealthCategory[])Enum.GetValues(typeof(ProjectHealthCategory)));
            ProjectHealthReport report = new ProjectHealthScanner().Scan(categories);
            Debug.Log(report.ToText());
        }

        public ProjectHealthReport Scan(ISet<ProjectHealthCategory> categories)
        {
            var context = new ProjectHealthContext();
            var report = new ProjectHealthReport();

            try
            {
                RunChecks(categories, context, report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                report.CheckedAssets = context.CheckedAssetCount;
            }

            return report;
        }

        private void RunChecks(
            ISet<ProjectHealthCategory> categories,
            ProjectHealthContext context,
            ProjectHealthReport report)
        {
            for (int i = 0; i < _checks.Length; i++)
            {
                IProjectHealthCheck check = _checks[i];
                if (!categories.Contains(check.Category))
                    continue;

                float progress = (float)i / _checks.Length;
                if (EditorUtility.DisplayCancelableProgressBar("Market Project Health", check.Name, progress))
                    break;

                RunCheck(check, context, report);
            }
        }

        private static void RunCheck(
            IProjectHealthCheck check,
            ProjectHealthContext context,
            ProjectHealthReport report)
        {
            try
            {
                check.Scan(context, report);
            }
            catch (Exception exception)
            {
                report.Add(new ProjectHealthIssue(
                    ProjectHealthSeverity.Error,
                    check.Category,
                    $"{check.Name} failed",
                    exception.Message,
                    ProjectHealthContext.ProjectRoot));
            }
        }
    }
}
