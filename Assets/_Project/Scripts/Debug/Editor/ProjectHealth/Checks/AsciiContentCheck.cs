using System;
using System.Collections.Generic;
using System.IO;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Finds non-ASCII text in project-owned code and serialized content.</summary>
    public sealed class AsciiContentCheck : IProjectHealthCheck
    {
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asset", ".prefab", ".unity"
        };

        public string Name => "ASCII content rule";
        public ProjectHealthCategory Category => ProjectHealthCategory.ProjectRules;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            string root = Path.GetFullPath(ProjectHealthContext.ProjectRoot);
            foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!TextExtensions.Contains(Path.GetExtension(file)))
                    continue;

                string assetPath = ToAssetPath(file);
                context.Track(assetPath);
                ScanFile(file, assetPath, report);
            }
        }

        private static void ScanFile(string file, string assetPath, ProjectHealthReport report)
        {
            try
            {
                int lineNumber = 0;
                foreach (string line in File.ReadLines(file))
                {
                    lineNumber++;
                    if (lineNumber == 1 && !IsTextFile(file, line))
                        return;
                    if (!ProjectHealthRules.HasNonAscii(line) && !ContainsEscapedCyrillic(line))
                        continue;

                    report.Add(new ProjectHealthIssue(
                        ProjectHealthSeverity.Error,
                        ProjectHealthCategory.ProjectRules,
                        "Non-ASCII project text",
                        "Replace inline non-ASCII text with ASCII English.",
                        assetPath,
                        lineNumber));
                    return;
                }
            }
            catch (IOException exception)
            {
                AddReadWarning(report, assetPath, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                AddReadWarning(report, assetPath, exception.Message);
            }
        }

        private static bool IsTextFile(string file, string firstLine)
        {
            return string.Equals(Path.GetExtension(file), ".cs", StringComparison.OrdinalIgnoreCase)
                || firstLine.StartsWith("%YAML", StringComparison.Ordinal);
        }

        private static bool ContainsEscapedCyrillic(string line)
        {
            for (int i = 0; i <= line.Length - 6; i++)
                if (line[i] == '\\' && line[i + 1] == 'u' && line[i + 2] == '0' && line[i + 3] == '4')
                    return true;

            return false;
        }

        private static string ToAssetPath(string file)
        {
            string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
            return relative.Replace('\\', '/');
        }

        private static void AddReadWarning(ProjectHealthReport report, string path, string message)
        {
            report.Add(new ProjectHealthIssue(
                ProjectHealthSeverity.Warning,
                ProjectHealthCategory.ProjectRules,
                "Could not inspect text file",
                message,
                path));
        }
    }
}
