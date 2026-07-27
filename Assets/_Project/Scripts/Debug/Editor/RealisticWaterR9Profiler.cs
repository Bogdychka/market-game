using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Profiles the R9 water costs as controlled fixed-view Editor Play Mode scenarios.
    /// </summary>
    [InitializeOnLoad]
    public static class RealisticWaterR9Profiler
    {
        private const string ScenePath = "Assets/_Project/Scenes/WaterShaderLab.unity";
        private const string MarketMaterialPath =
            "Assets/_Project/Art/Materials/Water/M_Ocean.mat";
        private const string PendingKey = "Market.RealisticWaterR9Profiler.Pending";
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const float WarmupSeconds = 1.5f;
        private const float MeasureSeconds = 5f;
        private const int SparseGridResolution = 64;

        private static readonly Vector3 FixedPosition = new(32f, 24f, -34f);
        private static readonly Vector3 FixedTarget = new(0f, -2f, 7f);
        private static readonly FrameTiming[] FrameTimings = new FrameTiming[1];
        private static readonly List<float> FrameSamples = new(1024);
        private static readonly List<float> CpuSamples = new(1024);
        private static readonly List<float> GpuSamples = new(1024);
        private static readonly List<ScenarioResult> Results = new(12);

        private static Camera _camera;
        private static UniversalAdditionalCameraData _cameraData;
        private static CameraOverrideOption _originalOpaqueOverride;
        private static UniversalRenderPipelineAsset _pipelineAsset;
        private static bool _originalOpaqueTexture;
        private static RenderTexture _target;
        private static Renderer _waterRenderer;
        private static MeshFilter _waterFilter;
        private static Mesh _originalMesh;
        private static Mesh _sparseMesh;
        private static Material _originalMaterial;
        private static Material _marketMaterial;
        private static RealisticWaterQualityController _quality;
        private static RealisticWaterPlanarReflection _reflection;
        private static RealisticWaterTemporalFoam _foam;
        private static RealisticWaterCausticProjection _caustics;
        private static int _scenarioIndex;
        private static double _scenarioStartedAt;
        private static int _lastFrame = -1;
        private static bool _previousRunInBackground;

        static RealisticWaterR9Profiler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ResumePendingProfile;
        }

        /// <summary>
        /// Runs the fixed R9 subsystem cost matrix and a full 360-degree High-tier camera turn.
        /// </summary>
        [MenuItem("Market/Debug/Water/Capture R9 Subsystem Profile")]
        public static void Run()
        {
            if (!EnsureLabScene())
                return;

            SessionState.SetBool(PendingKey, true);
            if (Application.isPlaying)
                BeginProfile();
            else
                EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(PendingKey, false))
            {
                BeginProfile();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopProfile();
            }
        }

        private static void ResumePendingProfile()
        {
            if (Application.isPlaying && SessionState.GetBool(PendingKey, false))
                BeginProfile();
        }

        private static bool EnsureLabScene()
        {
            if (Application.isPlaying)
                return SceneManager.GetActiveScene().path == ScenePath;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void BeginProfile()
        {
            if (_camera != null)
                return;
            if (!ResolveSceneComponents())
            {
                Debug.LogError(
                    "RealisticWaterR9Profiler: required WaterShaderLab components are missing.");
                FinishProfile();
                return;
            }

            ConfigureCamera();
            _sparseMesh = BuildSparseMesh();
            Results.Clear();
            _scenarioIndex = 0;
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            EditorApplication.update -= TickProfile;
            EditorApplication.update += TickProfile;
            StartScenario();
        }

        private static bool ResolveSceneComponents()
        {
            _camera = Camera.main;
            _quality = UnityEngine.Object.FindAnyObjectByType<
                RealisticWaterQualityController>(FindObjectsInactive.Include);
            if (_quality == null)
                return false;

            _waterRenderer = _quality.GetComponent<Renderer>();
            _waterFilter = _quality.GetComponent<MeshFilter>();
            _reflection = _quality.GetComponent<RealisticWaterPlanarReflection>();
            _foam = _quality.GetComponent<RealisticWaterTemporalFoam>();
            _caustics = _quality.GetComponent<RealisticWaterCausticProjection>();
            _originalMesh = _waterFilter != null ? _waterFilter.sharedMesh : null;
            _originalMaterial =
                _waterRenderer != null ? _waterRenderer.sharedMaterial : null;
            _marketMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MarketMaterialPath);
            return _camera != null &&
                _waterRenderer != null &&
                _waterFilter != null &&
                _originalMesh != null &&
                _originalMaterial != null &&
                _marketMaterial != null &&
                _reflection != null &&
                _foam != null &&
                _caustics != null;
        }

        private static void ConfigureCamera()
        {
            DisableCameraMotion(_camera.transform.root);
            SetFixedView();
            _target = new RenderTexture(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "RealisticWaterR9Profile",
                antiAliasing = 1,
            };
            _camera.targetTexture = _target;
            _cameraData = _camera.GetUniversalAdditionalCameraData();
            _originalOpaqueOverride = _cameraData.requiresColorOption;
            _pipelineAsset =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipelineAsset != null)
                _originalOpaqueTexture = _pipelineAsset.supportsCameraOpaqueTexture;
        }

        private static void StartScenario()
        {
            ProfileScenario scenario = (ProfileScenario)_scenarioIndex;
            ApplyScenario(scenario);
            FrameSamples.Clear();
            CpuSamples.Clear();
            GpuSamples.Clear();
            _lastFrame = -1;
            _scenarioStartedAt = EditorApplication.timeSinceStartup;
            FrameTimingManager.CaptureFrameTimings();
            Debug.Log($"RealisticWaterR9Profiler: starting {scenario}.");
        }

        private static void ApplyScenario(ProfileScenario scenario)
        {
            _waterRenderer.enabled = true;
            _waterFilter.sharedMesh = _originalMesh;
            _waterRenderer.sharedMaterial = _originalMaterial;
            SetOpaqueTexture(true);
            _quality.SetQuality(RealisticWaterQualityTier.Low);

            switch (scenario)
            {
                case ProfileScenario.RendererDisabled:
                    _waterRenderer.enabled = false;
                    break;
                case ProfileScenario.LowSparseOpaqueOff:
                    _waterFilter.sharedMesh = _sparseMesh;
                    SetOpaqueTexture(false);
                    break;
                case ProfileScenario.LowSparse:
                    _waterFilter.sharedMesh = _sparseMesh;
                    break;
                case ProfileScenario.ReflectionHalf:
                    _reflection.SetQuality(
                        WaterPlanarReflectionQuality.HalfResolution);
                    break;
                case ProfileScenario.TemporalFoam:
                    _foam.SetQuality(WaterFoamHistoryQuality.History256);
                    break;
                case ProfileScenario.ProjectedCaustics:
                    _caustics.SetQuality(WaterCausticQuality.ProjectedReceivers);
                    break;
                case ProfileScenario.MarketWater:
                    _waterRenderer.sharedMaterial = _marketMaterial;
                    break;
                case ProfileScenario.High:
                case ProfileScenario.HighCameraTurn:
                    _quality.SetQuality(RealisticWaterQualityTier.High);
                    break;
            }

            SetFixedView();
        }

        private static void TickProfile()
        {
            if (!Application.isPlaying || _camera == null)
                return;

            EditorApplication.QueuePlayerLoopUpdate();
            float elapsed =
                (float)(EditorApplication.timeSinceStartup - _scenarioStartedAt);
            if ((ProfileScenario)_scenarioIndex == ProfileScenario.HighCameraTurn)
                UpdateCameraTurn(elapsed);
            if (Time.frameCount == _lastFrame)
                return;

            _lastFrame = Time.frameCount;
            if (elapsed >= WarmupSeconds + MeasureSeconds)
            {
                CompleteScenario();
                return;
            }

            CaptureSample(elapsed);
        }

        private static void CaptureSample(float elapsed)
        {
            uint count = FrameTimingManager.GetLatestTimings(1, FrameTimings);
            FrameTimingManager.CaptureFrameTimings();
            if (elapsed < WarmupSeconds)
                return;

            FrameSamples.Add(Time.unscaledDeltaTime * 1000f);
            if (count == 0)
                return;
            if (FrameTimings[0].cpuFrameTime > 0.0)
                CpuSamples.Add((float)FrameTimings[0].cpuFrameTime);
            if (FrameTimings[0].gpuFrameTime > 0.0)
                GpuSamples.Add((float)FrameTimings[0].gpuFrameTime);
        }

        private static void CompleteScenario()
        {
            CaptureComparisonImage((ProfileScenario)_scenarioIndex);
            Results.Add(new ScenarioResult(
                (ProfileScenario)_scenarioIndex,
                CalculateStats(FrameSamples),
                CalculateStats(CpuSamples),
                CalculateStats(GpuSamples)));
            _scenarioIndex++;
            if (_scenarioIndex <= (int)ProfileScenario.HighCameraTurn)
            {
                StartScenario();
                return;
            }

            WriteReport();
            FinishProfile();
        }

        private static SampleStats CalculateStats(List<float> samples)
        {
            if (samples.Count == 0)
                return default;

            samples.Sort();
            float total = 0f;
            int overBudget = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                total += samples[i];
                if (samples[i] > 16.67f)
                    overBudget++;
            }

            int p95Index = Mathf.Clamp(
                Mathf.CeilToInt(samples.Count * 0.95f) - 1,
                0,
                samples.Count - 1);
            return new SampleStats(
                samples.Count,
                total / samples.Count,
                samples[p95Index],
                samples[samples.Count - 1],
                overBudget);
        }

        private static void SetOpaqueTexture(bool enabled)
        {
            _cameraData.requiresColorOption =
                enabled ? CameraOverrideOption.On : CameraOverrideOption.Off;
            if (_pipelineAsset != null)
                _pipelineAsset.supportsCameraOpaqueTexture = enabled;
        }

        private static void SetFixedView()
        {
            _camera.transform.position = FixedPosition;
            _camera.transform.rotation = Quaternion.LookRotation(
                FixedTarget - FixedPosition, Vector3.up);
            _camera.fieldOfView = 60f;
        }

        private static void UpdateCameraTurn(float elapsed)
        {
            float totalDuration = WarmupSeconds + MeasureSeconds;
            float angle = 360f * Mathf.Clamp01(elapsed / totalDuration);
            Vector3 offset = FixedPosition - FixedTarget;
            _camera.transform.position =
                FixedTarget + Quaternion.AngleAxis(angle, Vector3.up) * offset;
            _camera.transform.rotation = Quaternion.LookRotation(
                FixedTarget - _camera.transform.position, Vector3.up);
        }

        private static void WriteReport()
        {
            try
            {
                string folder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Artifacts",
                    "RealisticWater",
                    "R9");
                Directory.CreateDirectory(folder);
                File.WriteAllText(
                    Path.Combine(folder, "subsystem_profile.md"),
                    BuildReport());
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterR9Profiler: report failed: {exception.Message}");
            }
        }

        private static string BuildReport()
        {
            var report = new StringBuilder(4096);
            report.AppendLine("# R9 Realistic Water Subsystem Profile");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            report.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
            report.AppendLine($"Resolution: {CaptureWidth}x{CaptureHeight}");
            report.AppendLine(
                $"Per scenario: {WarmupSeconds:0.#} s warmup, " +
                $"{MeasureSeconds:0.#} s sample.");
            report.AppendLine();
            AppendScenarioTable(report);
            report.AppendLine();
            AppendCostDeltas(report);
            report.AppendLine();
            report.AppendLine(
                "All fixed-view scenarios use the R0 elevated camera. HighCameraTurn orbits the " +
                "same target through 360 degrees. Editor timings are comparative diagnostics.");
            return report.ToString();
        }

        private static void AppendScenarioTable(StringBuilder report)
        {
            report.AppendLine("## Scenario results");
            report.AppendLine();
            report.AppendLine(
                "| Scenario | Observed avg | Observed p95 | CPU p95 | GPU avg | GPU p95 | " +
                "GPU max | Frames >16.67 ms |");
            report.AppendLine(
                "|---|---:|---:|---:|---:|---:|---:|---:|");
            for (int i = 0; i < Results.Count; i++)
            {
                ScenarioResult result = Results[i];
                report.AppendLine(
                    $"| {result.Scenario} | {result.Frame.Average:0.00} | " +
                    $"{result.Frame.P95:0.00} | {result.Cpu.P95:0.00} | " +
                    $"{result.Gpu.Average:0.00} | {result.Gpu.P95:0.00} | " +
                    $"{result.Gpu.Maximum:0.00} | " +
                    $"{result.Frame.OverBudget}/{result.Frame.Count} |");
            }
        }

        private static void AppendCostDeltas(StringBuilder report)
        {
            report.AppendLine("## Isolated GPU average deltas");
            report.AppendLine();
            AppendDelta(
                report, "Core fragment-dominated water",
                ProfileScenario.RendererDisabled,
                ProfileScenario.LowSparseOpaqueOff);
            AppendDelta(
                report, "Opaque texture path",
                ProfileScenario.LowSparseOpaqueOff, ProfileScenario.LowSparse);
            AppendDelta(
                report, "Dense vertex grid",
                ProfileScenario.LowSparse, ProfileScenario.Low);
            AppendDelta(
                report, "Half-resolution reflection",
                ProfileScenario.Low, ProfileScenario.ReflectionHalf);
            AppendDelta(
                report, "Temporal foam history",
                ProfileScenario.Low, ProfileScenario.TemporalFoam);
            AppendDelta(
                report, "Projected caustic receivers",
                ProfileScenario.Low, ProfileScenario.ProjectedCaustics);
            AppendDelta(
                report, "Complete High tier",
                ProfileScenario.Low, ProfileScenario.High);
            AppendDelta(
                report, "High tier versus MarketWater",
                ProfileScenario.MarketWater, ProfileScenario.High);
        }

        private static void AppendDelta(
            StringBuilder report,
            string label,
            ProfileScenario baseline,
            ProfileScenario enabled)
        {
            ScenarioResult from = GetResult(baseline);
            ScenarioResult to = GetResult(enabled);
            report.AppendLine(
                $"- {label}: GPU avg " +
                $"{to.Gpu.Average - from.Gpu.Average:+0.00;-0.00;0.00} ms, " +
                $"GPU p95 {to.Gpu.P95 - from.Gpu.P95:+0.00;-0.00;0.00} ms.");
        }

        private static ScenarioResult GetResult(ProfileScenario scenario)
        {
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Scenario == scenario)
                    return Results[i];
            }

            return default;
        }

        private static void FinishProfile()
        {
            RestoreSceneState();
            StopProfile();
            SessionState.SetBool(PendingKey, false);
            if (Application.isPlaying)
                EditorApplication.isPlaying = false;
        }

        private static void RestoreSceneState()
        {
            if (_waterRenderer != null)
                _waterRenderer.enabled = true;
            if (_waterFilter != null && _originalMesh != null)
                _waterFilter.sharedMesh = _originalMesh;
            if (_waterRenderer != null && _originalMaterial != null)
                _waterRenderer.sharedMaterial = _originalMaterial;
            if (_quality != null)
                _quality.SetQuality(RealisticWaterQualityTier.High);
            if (_cameraData != null)
                _cameraData.requiresColorOption = _originalOpaqueOverride;
            if (_pipelineAsset != null)
                _pipelineAsset.supportsCameraOpaqueTexture = _originalOpaqueTexture;
        }

        private static void StopProfile()
        {
            EditorApplication.update -= TickProfile;
            if (_camera != null)
                _camera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
            }
            if (_sparseMesh != null)
                UnityEngine.Object.DestroyImmediate(_sparseMesh);

            Application.runInBackground = _previousRunInBackground;
            _camera = null;
            _cameraData = null;
            _pipelineAsset = null;
            _target = null;
            _sparseMesh = null;
        }

        private static void CaptureComparisonImage(ProfileScenario scenario)
        {
            if (scenario != ProfileScenario.MarketWater &&
                scenario != ProfileScenario.High)
            {
                return;
            }

            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                string folder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Artifacts",
                    "RealisticWater",
                    "R9",
                    "comparison_views");
                Directory.CreateDirectory(folder);
                RenderTexture.active = _target;
                image = new Texture2D(
                    CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(
                    new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply(false);
                string fileName = scenario == ProfileScenario.MarketWater
                    ? "market_water.png"
                    : "realistic_water_high.png";
                File.WriteAllBytes(
                    Path.Combine(folder, fileName),
                    image.EncodeToPNG());
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterR9Profiler: comparison capture failed: " +
                    exception.Message);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void DisableCameraMotion(Transform playerRoot)
        {
            foreach (Behaviour behaviour in
                     playerRoot.GetComponentsInChildren<Behaviour>(true))
            {
                string typeName = behaviour.GetType().Name;
                if (typeName == "FirstPersonController" || typeName == "HeadBob")
                    behaviour.enabled = false;
            }
        }

        private static Mesh BuildSparseMesh()
        {
            int row = SparseGridResolution + 1;
            var vertices = new Vector3[row * row];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[SparseGridResolution * SparseGridResolution * 6];
            PopulateSparseVertices(vertices, normals, uvs, row);
            PopulateSparseTriangles(triangles, row);
            var mesh = new Mesh { name = "Realistic Water R9 Sparse Grid" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateTangents();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 4f, 100f));
            return mesh;
        }

        private static void PopulateSparseVertices(
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            int row)
        {
            for (int z = 0; z < row; z++)
            {
                float z01 = z / (float)SparseGridResolution;
                for (int x = 0; x < row; x++)
                {
                    float x01 = x / (float)SparseGridResolution;
                    int index = z * row + x;
                    vertices[index] = new Vector3(
                        (x01 - 0.5f) * 100f, 0f, (z01 - 0.5f) * 100f);
                    normals[index] = Vector3.up;
                    uvs[index] = new Vector2(x01, z01);
                }
            }
        }

        private static void PopulateSparseTriangles(int[] triangles, int row)
        {
            int triangle = 0;
            for (int z = 0; z < SparseGridResolution; z++)
            {
                for (int x = 0; x < SparseGridResolution; x++)
                {
                    int bottomLeft = z * row + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + row;
                    int topRight = topLeft + 1;
                    triangles[triangle++] = bottomLeft;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = topRight;
                }
            }
        }

        private enum ProfileScenario
        {
            RendererDisabled = 0,
            LowSparseOpaqueOff = 1,
            LowSparse = 2,
            Low = 3,
            ReflectionHalf = 4,
            TemporalFoam = 5,
            ProjectedCaustics = 6,
            MarketWater = 7,
            High = 8,
            HighCameraTurn = 9,
        }

        private readonly struct SampleStats
        {
            public SampleStats(
                int count,
                float average,
                float p95,
                float maximum,
                int overBudget)
            {
                Count = count;
                Average = average;
                P95 = p95;
                Maximum = maximum;
                OverBudget = overBudget;
            }

            public int Count { get; }
            public float Average { get; }
            public float P95 { get; }
            public float Maximum { get; }
            public int OverBudget { get; }
        }

        private readonly struct ScenarioResult
        {
            public ScenarioResult(
                ProfileScenario scenario,
                SampleStats frame,
                SampleStats cpu,
                SampleStats gpu)
            {
                Scenario = scenario;
                Frame = frame;
                Cpu = cpu;
                Gpu = gpu;
            }

            public ProfileScenario Scenario { get; }
            public SampleStats Frame { get; }
            public SampleStats Cpu { get; }
            public SampleStats Gpu { get; }
        }
    }
}
