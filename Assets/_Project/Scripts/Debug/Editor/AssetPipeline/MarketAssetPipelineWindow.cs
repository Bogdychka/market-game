using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Market.DebugTools.Editor
{
    /// <summary>Focused UI Toolkit workflow for inspecting one imported model at a time.</summary>
    public sealed class MarketAssetPipelineWindow : EditorWindow
    {
        private ObjectField _assetField;
        private EnumField _profileField;
        private Label _status;
        private Label _metrics;
        private ScrollView _issues;
        private Button _staticPresetButton;
        private Button _createPrefabButton;
        private GameObject _model;
        private AssetPipelineReport _report;

        [MenuItem("Market Game/Asset Pipeline Assistant")]
        public static void ShowWindow()
        {
            MarketAssetPipelineWindow window = GetWindow<MarketAssetPipelineWindow>();
            window.titleContent = new GUIContent("Asset Pipeline");
            window.minSize = new Vector2(720f, 500f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            BuildHeader();
            BuildSelection();
            BuildActions();
            _issues = new ScrollView(ScrollViewMode.Vertical);
            _issues.style.flexGrow = 1f;
            _issues.style.marginTop = 8f;
            rootVisualElement.Add(_issues);
            RefreshView();
        }

        private void BuildHeader()
        {
            var title = new Label("Market Asset Pipeline Assistant");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);
            rootVisualElement.Add(new HelpBox(
                "Selected-model workflow only. Nothing changes until Apply Static Preset, Reimport, or Create Wrapper is explicitly pressed and confirmed.",
                HelpBoxMessageType.Info));
            _status = new Label("NO MODEL SELECTED");
            _status.style.unityFontStyleAndWeight = FontStyle.Bold;
            _metrics = new Label("Select an FBX or OBJ model asset.");
            rootVisualElement.Add(_status);
            rootVisualElement.Add(_metrics);
        }

        private void BuildSelection()
        {
            _assetField = new ObjectField("Model Asset")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            _assetField.RegisterValueChangedCallback(evt => SetModel(evt.newValue as GameObject));
            rootVisualElement.Add(_assetField);

            _profileField = new EnumField("Profile", AssetPipelineProfileId.StaticProp);
            _profileField.RegisterValueChangedCallback(_ => RefreshView());
            rootVisualElement.Add(_profileField);
        }

        private void BuildActions()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.Add(ActionButton("Use Selected Asset", UseSelected));
            row.Add(ActionButton("Analyze", Analyze));
            _staticPresetButton = ActionButton("Apply Static Preset", ApplyStaticPreset);
            row.Add(_staticPresetButton);
            _createPrefabButton = ActionButton("Create Wrapper Prefab", CreateWrapper);
            row.Add(_createPrefabButton);
            row.Add(ActionButton("Reimport", Reimport));
            row.Add(ActionButton("Copy Report", CopyReport));
            row.Add(ActionButton("Save Report", SaveReport));
            rootVisualElement.Add(row);
        }

        private static Button ActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6f;
            button.style.marginTop = 4f;
            return button;
        }

        private void UseSelected()
        {
            SetModel(Selection.activeObject as GameObject);
            _assetField.value = _model;
        }

        private void SetModel(GameObject candidate)
        {
            _model = AssetPipelineAnalyzer.IsModelAsset(candidate) ? candidate : null;
            _report = null;
            if (candidate != null && _model == null)
                ShowNotification(new GUIContent("Select an imported FBX or OBJ model asset."));
            RefreshView();
        }

        private void Analyze()
        {
            if (_model == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                var analyzer = new AssetPipelineAnalyzer();
                _report = analyzer.Analyze(_model, (AssetPipelineProfileId)_profileField.value);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"[AssetPipeline] Analysis failed: {exception.Message}");
                _report = null;
            }

            RefreshView();
        }

        private void ApplyStaticPreset()
        {
            if (_model == null || !CurrentProfile().IsStatic)
                return;
            if (AssetPipelineActions.ApplyStaticImporterPreset(_model))
                Analyze();
        }

        private void CreateWrapper()
        {
            if (_model == null)
                return;

            AssetPipelineActions.CreateWrapperPrefab(_model, CurrentProfile().IsStatic);
            Analyze();
        }

        private void Reimport()
        {
            if (_model == null)
                return;

            AssetPipelineActions.Reimport(_model);
            Analyze();
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
                string folder = Path.Combine(projectRoot, "AssetPipelineReports");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"asset-pipeline-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(file, _report.ToText());
                ShowNotification(new GUIContent($"Saved {Path.GetFileName(file)}"));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[AssetPipeline] Could not save report: {exception.Message}");
            }
        }

        private void RefreshView()
        {
            bool hasModel = _model != null;
            MarketAssetProfile profile = CurrentProfile();
            _staticPresetButton?.SetEnabled(hasModel && profile.IsStatic);
            _createPrefabButton?.SetEnabled(hasModel);

            if (_issues == null)
                return;
            _issues.Clear();

            if (_report == null)
            {
                _status.text = hasModel ? "READY TO ANALYZE" : "NO MODEL SELECTED";
                _status.style.color = Color.gray;
                _metrics.text = hasModel ? AssetDatabase.GetAssetPath(_model) : "Select an FBX or OBJ model asset.";
                return;
            }

            UpdateSummary();
            foreach (AssetPipelineIssue issue in _report.Issues)
                _issues.Add(BuildIssue(issue));
            if (_report.Issues.Count == 0)
                _issues.Add(new HelpBox("No issues found for the selected profile.", HelpBoxMessageType.Info));
        }

        private void UpdateSummary()
        {
            _status.text = _report.Status.ToString().ToUpperInvariant();
            _status.style.color = _report.Status switch
            {
                AssetPipelineStatus.Blocked => new Color(0.95f, 0.3f, 0.3f),
                AssetPipelineStatus.Warning => new Color(0.95f, 0.75f, 0.2f),
                _ => new Color(0.3f, 0.85f, 0.4f)
            };
            Vector3 size = _report.Dimensions;
            _metrics.text =
                $"{_report.AssetPath}\n" +
                $"Size {size.x:0.###} x {size.y:0.###} x {size.z:0.###} m | " +
                $"{_report.TriangleCount} tris | {_report.VertexCount} vertices | " +
                $"{_report.MaterialCount} materials | import scale {_report.ImportScale:0.###}";
        }

        private static VisualElement BuildIssue(AssetPipelineIssue issue)
        {
            var row = new VisualElement();
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            var title = new Label($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.Title}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(title);
            row.Add(new Label(issue.Description));
            return row;
        }

        private MarketAssetProfile CurrentProfile()
        {
            AssetPipelineProfileId id = _profileField == null
                ? AssetPipelineProfileId.StaticProp
                : (AssetPipelineProfileId)_profileField.value;
            return MarketAssetProfile.Get(id);
        }
    }
}
