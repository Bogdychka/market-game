using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Market.DebugTools.Editor
{
    /// <summary>UI Toolkit window for focused, read-only project validation.</summary>
    public sealed class ProjectHealthWindow : EditorWindow
    {
        private readonly Dictionary<ProjectHealthCategory, Toggle> _categoryToggles = new();
        private Label _statusLabel;
        private Label _summaryLabel;
        private Label _lastScanLabel;
        private ScrollView _results;
        private ProjectHealthReport _report;

        [MenuItem("Market Game/Project Health Scanner")]
        public static void ShowWindow()
        {
            ProjectHealthWindow window = GetWindow<ProjectHealthWindow>();
            window.titleContent = new GUIContent("Project Health");
            window.minSize = new Vector2(720f, 480f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            BuildHeader();
            BuildCategoryRow();
            BuildActionRow();
            _results = new ScrollView(ScrollViewMode.Vertical);
            _results.style.flexGrow = 1f;
            _results.style.marginTop = 8f;
            rootVisualElement.Add(_results);
            RefreshView();
        }

        private void BuildHeader()
        {
            var title = new Label("Market Project Health");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            _statusLabel = new Label("NOT SCANNED");
            _statusLabel.style.fontSize = 15f;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_statusLabel);

            _summaryLabel = new Label("Errors 0 | Warnings 0 | Info 0 | Checked 0");
            _lastScanLabel = new Label("Last scan: never");
            rootVisualElement.Add(_summaryLabel);
            rootVisualElement.Add(_lastScanLabel);
            rootVisualElement.Add(new HelpBox(
                "Read-only checks: data contracts, missing prefab scripts, ASCII content, and serialized outdoor rendering budgets. Scene loading and heuristic null-reference scans are intentionally excluded.",
                HelpBoxMessageType.Info));
        }

        private void BuildCategoryRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8f;
            foreach (ProjectHealthCategory category in Enum.GetValues(typeof(ProjectHealthCategory)))
            {
                var toggle = new Toggle(category.ToString()) { value = true };
                toggle.style.marginRight = 14f;
                _categoryToggles.Add(category, toggle);
                row.Add(toggle);
            }

            rootVisualElement.Add(row);
        }

        private void BuildActionRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6f;
            row.Add(ActionButton("Run Scan", RunScan));
            row.Add(ActionButton("Clear Results", ClearResults));
            row.Add(ActionButton("Copy Report", CopyReport));
            row.Add(ActionButton("Save Report", SaveReport));
            rootVisualElement.Add(row);
        }

        private static Button ActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6f;
            return button;
        }

        private void RunScan()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ShowNotification(new GUIContent("Exit Play Mode before scanning."));
                return;
            }

            _report = new ProjectHealthScanner().Scan(SelectedCategories());
            _lastScanLabel.text = $"Last scan: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            RefreshView();
        }

        private HashSet<ProjectHealthCategory> SelectedCategories()
        {
            var selected = new HashSet<ProjectHealthCategory>();
            foreach (KeyValuePair<ProjectHealthCategory, Toggle> pair in _categoryToggles)
                if (pair.Value.value)
                    selected.Add(pair.Key);

            return selected;
        }

        private void ClearResults()
        {
            _report = null;
            _lastScanLabel.text = "Last scan: never";
            RefreshView();
        }

        private void CopyReport()
        {
            if (_report == null)
                return;

            EditorGUIUtility.systemCopyBuffer = _report.ToText();
            ShowNotification(new GUIContent("Report copied."));
        }

        private void SaveReport()
        {
            if (_report == null)
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            try
            {
                string folder = Path.Combine(projectRoot, "ProjectHealthReports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"market-health-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, _report.ToText());
                ShowNotification(new GUIContent($"Saved {Path.GetFileName(path)}"));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[ProjectHealth] Could not save report: {exception.Message}");
            }
        }

        private void RefreshView()
        {
            _results.Clear();
            if (_report == null)
            {
                _statusLabel.text = "NOT SCANNED";
                _statusLabel.style.color = Color.gray;
                _summaryLabel.text = "Errors 0 | Warnings 0 | Info 0 | Checked 0";
                return;
            }

            UpdateSummary();
            foreach (ProjectHealthIssue issue in _report.Issues)
                _results.Add(BuildIssueRow(issue));
            if (_report.Issues.Count == 0)
                _results.Add(new HelpBox("No issues found by the selected checks.", HelpBoxMessageType.Info));
        }

        private void UpdateSummary()
        {
            _statusLabel.text = _report.Status.ToString().ToUpperInvariant();
            _statusLabel.style.color = _report.Status switch
            {
                ProjectHealthStatus.Red => new Color(0.95f, 0.3f, 0.3f),
                ProjectHealthStatus.Yellow => new Color(0.95f, 0.75f, 0.2f),
                _ => new Color(0.3f, 0.85f, 0.4f)
            };
            _summaryLabel.text =
                $"Errors {_report.ErrorCount} | Warnings {_report.WarningCount} | " +
                $"Info {_report.InfoCount} | Checked {_report.CheckedAssets}";
        }

        private static VisualElement BuildIssueRow(ProjectHealthIssue issue)
        {
            var row = new VisualElement();
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;

            var heading = new Label($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.Title}");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(heading);
            row.Add(new Label($"{issue.Category} | {FormatPath(issue)}"));
            row.Add(new Label(issue.Description));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(ActionButton("Select", () => SelectAsset(issue.AssetPath)));
            buttons.Add(ActionButton("Ping", () => PingAsset(issue.AssetPath)));
            row.Add(buttons);
            return row;
        }

        private static string FormatPath(ProjectHealthIssue issue)
        {
            return issue.Line > 0 ? $"{issue.AssetPath}:{issue.Line}" : issue.AssetPath;
        }

        private static void SelectAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
                Selection.activeObject = asset;
        }

        private static void PingAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }
    }
}
