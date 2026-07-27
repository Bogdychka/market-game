using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Market.DebugTools
{
    /// <summary>
    /// Captures repeatable realistic-water review views and an indicative Editor Play Mode
    /// frame-time baseline. Output is written under the git-ignored Artifacts folder.
    /// </summary>
    [InitializeOnLoad]
    public static class RealisticWaterBaseline
    {
        private const string ScenePath = "Assets/_Project/Scenes/WaterShaderLab.unity";
        private const string MaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterLab.mat";
        private const string ShaderPath =
            "Assets/_Project/Art/Materials/Water/RealisticWater.shader";
        private const string ProjectedCausticMaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterProjectedCaustics.mat";
        private const string UnderwaterSurfaceMaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterUnderwaterSurface.mat";
        private const string UnderwaterSurfaceShaderPath =
            "Assets/_Project/Art/Shaders/RealisticWaterUnderwaterSurface.shader";
        private const string PendingKey = "Market.RealisticWaterBaseline.Pending";
        private const string UnderwaterPendingKey =
            "Market.RealisticWaterBaseline.UnderwaterPending";
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const float WarmupSeconds = 2f;
        private const float MeasureSeconds = 8f;

        private static readonly BaselineView[] Views =
        {
            new(
                "01_elevated_overview.png",
                new Vector3(32f, 24f, -34f),
                new Vector3(0f, -2f, 7f)),
            new(
                "02_shoreline_detail.png",
                new Vector3(-18f, 3.2f, -34f),
                new Vector3(-6f, -0.2f, -10f)),
            new(
                "03_horizon_aliasing.png",
                new Vector3(-42f, 2.4f, 20f),
                new Vector3(42f, 1.2f, 20f)),
        };

        private static readonly BaselineView UnderwaterPerformanceView = new(
            "r8_underwater_performance",
            new Vector3(0f, -0.65f, 20f),
            new Vector3(42f, 0.2f, 20f));

        private static readonly List<float> FrameSamples = new(1024);
        private static readonly List<float> CpuSamples = new(1024);
        private static readonly List<float> GpuSamples = new(1024);
        private static readonly FrameTiming[] FrameTimings = new FrameTiming[1];

        private static Camera _benchmarkCamera;
        private static RenderTexture _benchmarkTarget;
        private static double _startedAt;
        private static int _lastFrame = -1;
        private static bool _previousRunInBackground;
        private static bool _changedRunInBackground;
        private static BenchmarkMode _benchmarkMode;
        private static PerformanceResult? _underwaterFallbackResult;
        private static RealisticWaterUnderwaterSurface _underwaterSurface;

        static RealisticWaterBaseline()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ResumePendingBenchmark;
        }

        /// <summary>Captures the R0 reference views, then measures a fixed elevated view.</summary>
        [MenuItem("Market/Debug/Water/Capture R0 Baseline")]
        public static void Run()
        {
            if (!EnsureLabScene())
                return;

            CaptureReferenceViews(true);
            SessionState.SetBool(PendingKey, true);
            if (Application.isPlaying)
                BeginBenchmark();
            else
                EditorApplication.isPlaying = true;
        }

        /// <summary>Captures the three fixed review views without entering Play Mode.</summary>
        [MenuItem("Market/Debug/Water/Capture R0 Views Only")]
        public static void CaptureViewsOnly()
        {
            if (EnsureLabScene())
                CaptureReferenceViews(false);
        }

        /// <summary>
        /// Measures the R8 underside pass against its front-face-only fallback from the same view.
        /// </summary>
        [MenuItem("Market/Debug/Water/Capture R8 Underwater Performance")]
        public static void CaptureR8UnderwaterPerformance()
        {
            if (!EnsureLabScene())
                return;

            SessionState.SetBool(UnderwaterPendingKey, true);
            SessionState.SetBool(PendingKey, true);
            if (Application.isPlaying)
                BeginBenchmark();
            else
                EditorApplication.isPlaying = true;
        }

        [MenuItem("Market/Debug/Water/Capture R6 Foam Buffers")]
        public static void CaptureR6FoamBuffers()
        {
            RealisticWaterTemporalFoam foam =
                UnityEngine.Object.FindAnyObjectByType<RealisticWaterTemporalFoam>(
                    FindObjectsInactive.Include);
            if (foam == null ||
                foam.HistoryTexture == null ||
                foam.ShorelineMaskTexture == null)
            {
                Debug.LogError(
                    "RealisticWaterBaseline: R6 foam buffers are unavailable. " +
                    "Run WaterShaderLab in Play Mode first.");
                return;
            }

            string outputFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Artifacts",
                "RealisticWater",
                "R6",
                "diagnostics");
            Directory.CreateDirectory(outputFolder);
            WriteDiagnosticTexture(
                foam.HistoryTexture,
                Path.Combine(outputFolder, "foam_history_rg.png"),
                false);
            WriteDiagnosticTexture(
                foam.ShorelineMaskTexture,
                Path.Combine(outputFolder, "shoreline_mask.png"),
                true);
            Debug.Log(
                $"RealisticWaterBaseline: captured R6 foam buffers at {outputFolder}.");
        }

        [MenuItem("Market/Debug/Water/R6/Enable Whitecap Injection")]
        public static void EnableR6WhitecapInjection()
        {
            SetR6WhitecapInjection(true);
        }

        [MenuItem("Market/Debug/Water/R6/Suppress Whitecap Injection")]
        public static void SuppressR6WhitecapInjection()
        {
            SetR6WhitecapInjection(false);
        }

        private static void SetR6WhitecapInjection(bool enabled)
        {
            RealisticWaterTemporalFoam foam =
                UnityEngine.Object.FindAnyObjectByType<RealisticWaterTemporalFoam>(
                    FindObjectsInactive.Include);
            if (foam == null)
            {
                Debug.LogError(
                    "RealisticWaterBaseline: R6 temporal foam component is missing.");
                return;
            }

            foam.SetWhitecapInjectionEnabled(enabled);
            Debug.Log(
                $"RealisticWaterBaseline: whitecap injection " +
                $"{(enabled ? "enabled" : "suppressed")} without a history reset.");
        }

        private static bool EnsureLabScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == ScenePath)
                return true;

            if (scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void CaptureReferenceViews(bool writePendingReport)
        {
            Camera camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException(
                    "RealisticWaterBaseline: WaterShaderLab has no Main Camera.");

            string outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            GameObject label = GameObject.Find("Label - Water Shader Lab");
            bool labelWasActive = label != null && label.activeSelf;

            try
            {
                if (label != null)
                    label.SetActive(false);

                foreach (BaselineView view in Views)
                {
                    SetView(camera, view);
                    RenderCamera(camera, Path.Combine(outputFolder, view.FileName));
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                if (label != null)
                    label.SetActive(labelWasActive);
            }

            if (writePendingReport)
                WriteReport(null);
            Debug.Log(
                $"RealisticWaterBaseline: captured {Views.Length} R0 views at " +
                $"{CaptureWidth}x{CaptureHeight} in {outputFolder}.");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(PendingKey, false))
            {
                BeginBenchmark();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopSampling();
            }
        }

        private static void ResumePendingBenchmark()
        {
            if (Application.isPlaying && SessionState.GetBool(PendingKey, false))
                BeginBenchmark();
        }

        private static void BeginBenchmark()
        {
            if (_benchmarkCamera != null)
                return;

            bool underwaterRun =
                SessionState.GetBool(UnderwaterPendingKey, false);
            _benchmarkMode = underwaterRun
                ? BenchmarkMode.UnderwaterFallback
                : BenchmarkMode.Standard;
            _benchmarkCamera = Camera.main;
            if (_benchmarkCamera == null)
            {
                Debug.LogError("RealisticWaterBaseline: Main Camera not found in Play Mode.");
                FinishBenchmark();
                return;
            }

            DisableCameraMotion(_benchmarkCamera.transform.root);
            if (underwaterRun)
            {
                _underwaterSurface =
                    UnityEngine.Object.FindAnyObjectByType<
                        RealisticWaterUnderwaterSurface>(
                        FindObjectsInactive.Include);
                if (_underwaterSurface == null)
                {
                    Debug.LogError(
                        "RealisticWaterBaseline: R8 underwater surface not found.");
                    FinishBenchmark();
                    return;
                }

                _underwaterSurface.SetQuality(
                    WaterUnderwaterSurfaceQuality.FrontFaceOnly);
                _underwaterSurface.SetTransitionState(true, 1f);
                SetView(_benchmarkCamera, UnderwaterPerformanceView);
            }
            else
            {
                SetView(_benchmarkCamera, Views[0]);
            }

            _benchmarkTarget = new RenderTexture(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                name = underwaterRun
                    ? "RealisticWaterR8UnderwaterBenchmark"
                    : "RealisticWaterR0Benchmark"
            };
            _benchmarkCamera.targetTexture = _benchmarkTarget;

            BeginSamplingWindow();
        }

        private static void BeginSamplingWindow()
        {
            FrameSamples.Clear();
            CpuSamples.Clear();
            GpuSamples.Clear();
            _previousRunInBackground = Application.runInBackground;
            _changedRunInBackground = true;
            Application.runInBackground = true;
            _startedAt = EditorApplication.timeSinceStartup;
            _lastFrame = -1;
            FrameTimingManager.CaptureFrameTimings();
            EditorApplication.update -= TickBenchmark;
            EditorApplication.update += TickBenchmark;
        }

        private static void TickBenchmark()
        {
            if (!Application.isPlaying || _benchmarkCamera == null)
                return;

            EditorApplication.QueuePlayerLoopUpdate();
            float elapsed =
                (float)(EditorApplication.timeSinceStartup - _startedAt);
            if (elapsed >= WarmupSeconds + MeasureSeconds + 5f)
            {
                Debug.LogWarning(
                    "RealisticWaterBaseline: sampling reached the safety timeout; " +
                    "available frame timings will be reported.");
                PerformanceResult timeoutResult = BuildPerformanceResult();
                CompleteBenchmarkPhase(timeoutResult);
                return;
            }

            if (Time.frameCount == _lastFrame)
                return;

            _lastFrame = Time.frameCount;
            if (elapsed >= WarmupSeconds + MeasureSeconds)
            {
                PerformanceResult result = BuildPerformanceResult();
                CompleteBenchmarkPhase(result);
                return;
            }

            uint timingCount = FrameTimingManager.GetLatestTimings(1, FrameTimings);
            FrameTimingManager.CaptureFrameTimings();
            if (elapsed < WarmupSeconds)
                return;

            FrameSamples.Add(Time.unscaledDeltaTime * 1000f);
            if (timingCount == 0)
                return;

            if (FrameTimings[0].cpuFrameTime > 0.0)
                CpuSamples.Add((float)FrameTimings[0].cpuFrameTime);
            if (FrameTimings[0].gpuFrameTime > 0.0)
                GpuSamples.Add((float)FrameTimings[0].gpuFrameTime);
        }

        private static void CompleteBenchmarkPhase(PerformanceResult result)
        {
            if (_benchmarkMode == BenchmarkMode.UnderwaterFallback)
            {
                _underwaterFallbackResult = result;
                _benchmarkMode = BenchmarkMode.UnderwaterSurface;
                _underwaterSurface.SetQuality(
                    WaterUnderwaterSurfaceQuality.UnderwaterSurface);
                _underwaterSurface.SetTransitionState(true, 1f);
                Debug.Log(
                    "RealisticWaterBaseline: R8 fallback sample complete; " +
                    "starting the underside-pass sample.");
                BeginSamplingWindow();
                return;
            }

            if (_benchmarkMode == BenchmarkMode.UnderwaterSurface)
            {
                WriteUnderwaterPerformanceReport(
                    _underwaterFallbackResult, result);
            }
            else
            {
                WriteReport(result);
            }

            Debug.Log(result.ToLogLine());
            FinishBenchmark();
        }

        private static PerformanceResult BuildPerformanceResult()
        {
            return new PerformanceResult(
                CalculateStats(FrameSamples),
                CalculateStats(CpuSamples),
                CalculateStats(GpuSamples));
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
                Mathf.CeilToInt(samples.Count * 0.95f) - 1, 0, samples.Count - 1);
            return new SampleStats(
                samples.Count,
                total / samples.Count,
                samples[p95Index],
                samples[samples.Count - 1],
                overBudget);
        }

        private static void FinishBenchmark()
        {
            if (_underwaterSurface != null)
            {
                _underwaterSurface.SetQuality(
                    WaterUnderwaterSurfaceQuality.UnderwaterSurface);
                _underwaterSurface.SetTransitionState(false, 0f);
            }

            StopSampling();
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(UnderwaterPendingKey, false);
            _benchmarkMode = BenchmarkMode.Standard;
            _underwaterFallbackResult = null;
            _underwaterSurface = null;
            if (Application.isPlaying)
                EditorApplication.isPlaying = false;
        }

        private static void StopSampling()
        {
            EditorApplication.update -= TickBenchmark;
            if (_benchmarkCamera != null)
                _benchmarkCamera.targetTexture = null;
            if (_benchmarkTarget != null)
            {
                _benchmarkTarget.Release();
                UnityEngine.Object.DestroyImmediate(_benchmarkTarget);
            }

            if (_changedRunInBackground)
                Application.runInBackground = _previousRunInBackground;
            _changedRunInBackground = false;
            _benchmarkCamera = null;
            _benchmarkTarget = null;
        }

        private static void DisableCameraMotion(Transform playerRoot)
        {
            foreach (Behaviour behaviour in playerRoot.GetComponentsInChildren<Behaviour>(true))
            {
                string typeName = behaviour.GetType().Name;
                if (typeName == "FirstPersonController" || typeName == "HeadBob")
                    behaviour.enabled = false;
            }
        }

        private static void SetView(Camera camera, BaselineView view)
        {
            camera.transform.position = view.Position;
            camera.transform.rotation = Quaternion.LookRotation(
                view.Target - view.Position, Vector3.up);
            camera.fieldOfView = 60f;
        }

        private static void RenderCamera(Camera camera, string outputPath)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D image = new(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply(false);
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void WriteDiagnosticTexture(
            Texture source, string outputPath, bool grayscale)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            Texture2D image = new(
                source.width, source.height, TextureFormat.RGBA32, false, true);

            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                image.ReadPixels(
                    new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply(false);
                if (grayscale)
                    ConvertRedToGrayscale(image);
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void ConvertRedToGrayscale(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = pixels[i].r;
                pixels[i] = new Color32(value, value, value, 255);
            }

            image.SetPixels32(pixels);
            image.Apply(false);
        }

        private static void WriteReport(PerformanceResult? performance)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var report = new StringBuilder(4096);
            report.AppendLine("# Realistic Water R0 Baseline");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Scene: `{ScenePath}`");
            report.AppendLine($"Capture resolution: {CaptureWidth}x{CaptureHeight}");
            report.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
            report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            report.AppendLine($"OS: {SystemInfo.operatingSystem}");
            report.AppendLine(
                $"Shader dependency hash: `{AssetDatabase.GetAssetDependencyHash(ShaderPath)}`");
            report.AppendLine(
                $"Material dependency hash: `{AssetDatabase.GetAssetDependencyHash(MaterialPath)}`");
            report.AppendLine();
            report.AppendLine("## Views");
            report.AppendLine();
            foreach (BaselineView view in Views)
            {
                report.AppendLine(
                    $"- `{view.FileName}`: position {Format(view.Position)}, " +
                    $"target {Format(view.Target)}, FOV 60.");
            }

            report.AppendLine();
            report.AppendLine(
                "Camera transforms are deterministic; the animated wave phase remains live.");
            report.AppendLine();
            AppendMaterialSnapshot(report, material);
            report.AppendLine();
            AppendTemporalFoamSnapshot(report);
            report.AppendLine();
            AppendProjectedCausticSnapshot(report);
            report.AppendLine();
            AppendUnderwaterSurfaceSnapshot(report);
            report.AppendLine();
            AppendQualityTierSnapshot(report);
            report.AppendLine();
            report.AppendLine("## Performance");
            report.AppendLine();
            report.AppendLine(
                $"Editor Play Mode, fixed `{Views[0].FileName}` view, " +
                $"{CaptureWidth}x{CaptureHeight}, {WarmupSeconds:0.#} s warmup, " +
                $"{MeasureSeconds:0.#} s sample.");
            report.AppendLine();
            if (performance.HasValue)
                performance.Value.AppendTo(report);
            else
                report.AppendLine("Pending.");

            string outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "baseline.md"), report.ToString());
        }

        private static void WriteUnderwaterPerformanceReport(
            PerformanceResult? fallback,
            PerformanceResult underside)
        {
            var report = new StringBuilder(2048);
            report.AppendLine("# R8 Underwater Surface Pass Performance");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Scene: `{ScenePath}`");
            report.AppendLine($"Capture resolution: {CaptureWidth}x{CaptureHeight}");
            report.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
            report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            report.AppendLine(
                $"Underwater shader dependency hash: " +
                $"`{AssetDatabase.GetAssetDependencyHash(UnderwaterSurfaceShaderPath)}`");
            report.AppendLine(
                $"Underwater material dependency hash: " +
                $"`{AssetDatabase.GetAssetDependencyHash(UnderwaterSurfaceMaterialPath)}`");
            report.AppendLine();
            report.AppendLine("## Fixed view");
            report.AppendLine();
            report.AppendLine(
                $"- Position {Format(UnderwaterPerformanceView.Position)}, target " +
                $"{Format(UnderwaterPerformanceView.Target)}, FOV 60.");
            report.AppendLine(
                $"- Each phase uses {WarmupSeconds:0.#} s warmup and " +
                $"{MeasureSeconds:0.#} s sampling.");
            report.AppendLine();
            report.AppendLine("## Front-face-only fallback");
            report.AppendLine();
            if (fallback.HasValue)
                fallback.Value.AppendTo(report);
            else
                report.AppendLine("Unavailable.");
            report.AppendLine();
            report.AppendLine("## Underwater surface enabled");
            report.AppendLine();
            underside.AppendTo(report);
            report.AppendLine();
            report.AppendLine("## Additional pass cost");
            report.AppendLine();
            if (fallback.HasValue)
            {
                AppendPerformanceDelta(
                    report, "Observed frame time",
                    fallback.Value.Frame, underside.Frame);
                AppendPerformanceDelta(
                    report, "FrameTimingManager CPU",
                    fallback.Value.Cpu, underside.Cpu);
                AppendPerformanceDelta(
                    report, "FrameTimingManager GPU",
                    fallback.Value.Gpu, underside.Gpu);
            }
            else
            {
                report.AppendLine("Fallback sample unavailable; delta cannot be calculated.");
            }

            report.AppendLine();
            report.AppendLine(
                "The low quality fallback keeps the underside renderer disabled. " +
                "Editor Play Mode timings are comparative diagnostics, not a standalone-build " +
                "performance guarantee.");

            string outputFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Artifacts",
                "RealisticWater",
                "R8");
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(
                Path.Combine(outputFolder, "underwater_performance.md"),
                report.ToString());
        }

        private static void AppendPerformanceDelta(
            StringBuilder report,
            string label,
            SampleStats fallback,
            SampleStats underside)
        {
            if (!fallback.Available || !underside.Available)
            {
                report.AppendLine($"- {label}: unavailable.");
                return;
            }

            report.AppendLine(
                $"- {label}: avg " +
                $"{underside.Average - fallback.Average:+0.00;-0.00;0.00} ms, " +
                $"p95 {underside.P95 - fallback.P95:+0.00;-0.00;0.00} ms, " +
                $"max {underside.Maximum - fallback.Maximum:+0.00;-0.00;0.00} ms.");
        }

        private static void AppendMaterialSnapshot(StringBuilder report, Material material)
        {
            report.AppendLine("## Material snapshot");
            report.AppendLine();
            if (material == null)
            {
                report.AppendLine("Material missing.");
                return;
            }

            report.AppendLine($"Shader: `{material.shader.name}`");
            report.AppendLine($"Render queue: {material.renderQueue}");
            AppendVector(report, material, "_WindDirection");
            AppendFloat(report, material, "_WindSpread");
            AppendVector(report, material, "_Wave1Params");
            AppendFloat(report, material, "_Wave1Steepness");
            AppendVector(report, material, "_Wave2Params");
            AppendFloat(report, material, "_Wave2Steepness");
            AppendVector(report, material, "_Wave3Params");
            AppendFloat(report, material, "_Wave3Steepness");
            AppendVector(report, material, "_Wave4Params");
            AppendFloat(report, material, "_Wave4Steepness");
            AppendTexture(report, material, "_NormalMapA");
            AppendTexture(report, material, "_NormalMapB");
            AppendFloat(report, material, "_NormalLayerATiling");
            AppendFloat(report, material, "_NormalLayerBTiling");
            AppendFloat(report, material, "_NormalLayerASpeed");
            AppendFloat(report, material, "_NormalLayerBSpeed");
            AppendFloat(report, material, "_NormalLayerBRotation");
            AppendFloat(report, material, "_MicroWaveStrength");
            AppendFloat(report, material, "_DetailFadeStart");
            AppendFloat(report, material, "_DetailFadeEnd");
            AppendFloat(report, material, "_RefractionStrength");
            AppendFloat(report, material, "_RefractionEdgeFade");
            AppendFloat(report, material, "_RefractionDepthScale");
            AppendFloat(report, material, "_DepthFadeDistance");
            AppendVector(report, material, "_AbsorptionCoefficients");
            AppendColor(report, material, "_ScatteringColor");
            AppendFloat(report, material, "_ScatteringStrength");
            AppendFloat(report, material, "_FoamCrestGain");
            AppendFloat(report, material, "_FoamCrestBias");
            AppendFloat(report, material, "_FoamShoreWidth");
            AppendFloat(report, material, "_FoamCrestStrength");
            AppendFloat(report, material, "_FoamShoreStrength");
            AppendFloat(report, material, "_CausticIntensity");
            AppendFloat(report, material, "_FresnelPower");
            AppendFloat(report, material, "_FresnelBase");
            AppendFloat(report, material, "_SpecPower");
            AppendFloat(report, material, "_SpecStrength");
            AppendFloat(report, material, "_Roughness");
            AppendFloat(report, material, "_ReflectionStrength");
            AppendFloat(report, material, "_PlanarReflectionStrength");
            AppendFloat(report, material, "_ReflectionEdgeFade");
        }

        private static void AppendTemporalFoamSnapshot(StringBuilder report)
        {
            report.AppendLine("## Temporal foam history");
            report.AppendLine();
            RealisticWaterTemporalFoam foam =
                UnityEngine.Object.FindAnyObjectByType<RealisticWaterTemporalFoam>(
                    FindObjectsInactive.Include);
            if (foam == null)
            {
                report.AppendLine("Component missing; shader uses the no-history fallback.");
                return;
            }

            Vector2 coverage = foam.WorldCoverage;
            report.AppendLine($"- Quality: `{foam.Quality}`.");
            report.AppendLine($"- Resolution: {foam.ActiveResolution}x{foam.ActiveResolution}.");
            report.AppendLine(
                $"- World coverage: {coverage.x:0.##}x{coverage.y:0.##} units.");
            report.AppendLine(
                $"- Estimated history + shoreline memory: " +
                $"{foam.EstimatedMemoryBytes / 1048576f:0.###} MiB.");
        }

        private static void AppendProjectedCausticSnapshot(StringBuilder report)
        {
            report.AppendLine("## World-space caustic projection");
            report.AppendLine();
            RealisticWaterCausticProjection projection =
                UnityEngine.Object.FindAnyObjectByType<RealisticWaterCausticProjection>(
                    FindObjectsInactive.Include);
            if (projection == null)
            {
                report.AppendLine(
                    "Component missing; shader uses the surface-composite fallback.");
                return;
            }

            report.AppendLine($"- Quality: `{projection.Quality}`.");
            report.AppendLine($"- Receiver overlays: {projection.ReceiverCount}.");
            report.AppendLine(
                $"- Projected path available: {projection.ProjectedPathAvailable}.");
            report.AppendLine(
                $"- Bounds: {Format(projection.ProjectionBounds)}.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                ProjectedCausticMaterialPath);
            if (material == null)
                return;

            AppendFloat(report, material, "_CausticIntensity");
            AppendFloat(report, material, "_CausticTilingA");
            AppendFloat(report, material, "_CausticTilingB");
            AppendFloat(report, material, "_CausticDepthStart");
            AppendFloat(report, material, "_CausticDepthEnd");
            AppendFloat(report, material, "_CausticTurbidity");
        }

        private static void AppendUnderwaterSurfaceSnapshot(StringBuilder report)
        {
            report.AppendLine("## Underwater surface and volume");
            report.AppendLine();
            RealisticWaterUnderwaterSurface surface =
                UnityEngine.Object.FindAnyObjectByType<RealisticWaterUnderwaterSurface>(
                    FindObjectsInactive.Include);
            UnderwaterFogController fog =
                UnityEngine.Object.FindAnyObjectByType<UnderwaterFogController>(
                    FindObjectsInactive.Include);
            if (surface == null || fog == null)
            {
                report.AppendLine(
                    "R8 components missing; water remains front-face-only.");
                return;
            }

            report.AppendLine($"- Quality: `{surface.Quality}`.");
            report.AppendLine(
                $"- Underwater renderer enabled: {surface.UnderwaterRendererEnabled}.");
            report.AppendLine(
                $"- Transition half-height: {fog.TransitionHalfHeight:0.###} units.");
            report.AppendLine(
                $"- Underwater fog density: {fog.UnderwaterFogDensity:0.###}.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                UnderwaterSurfaceMaterialPath);
            if (material == null)
                return;

            AppendFloat(report, material, "_InternalReflectionStrength");
            AppendFloat(report, material, "_WaterIOR");
            AppendColor(report, material, "_UnderwaterFogColor");
        }

        private static void AppendQualityTierSnapshot(StringBuilder report)
        {
            report.AppendLine("## Coordinated quality tier");
            report.AppendLine();
            RealisticWaterQualityController controller =
                UnityEngine.Object.FindAnyObjectByType<
                    RealisticWaterQualityController>(
                    FindObjectsInactive.Include);
            if (controller == null)
            {
                report.AppendLine("Quality controller missing.");
                return;
            }

            report.AppendLine($"- Tier: `{controller.QualityTier}`.");
            report.AppendLine(
                $"- Configuration valid: {controller.IsConfigurationValid}.");
            RealisticWaterPlanarReflection reflection =
                controller.GetComponent<RealisticWaterPlanarReflection>();
            RealisticWaterTemporalFoam foam =
                controller.GetComponent<RealisticWaterTemporalFoam>();
            RealisticWaterCausticProjection caustics =
                controller.GetComponent<RealisticWaterCausticProjection>();
            RealisticWaterUnderwaterSurface underwater =
                controller.GetComponent<RealisticWaterUnderwaterSurface>();
            report.AppendLine($"- Reflection: `{reflection?.Quality}`.");
            report.AppendLine($"- Foam: `{foam?.Quality}`.");
            report.AppendLine($"- Caustics: `{caustics?.Quality}`.");
            report.AppendLine($"- Underwater: `{underwater?.Quality}`.");
        }

        private static void AppendFloat(StringBuilder report, Material material, string property)
        {
            if (material.HasProperty(property))
                report.AppendLine($"- `{property}`: {material.GetFloat(property):0.####}");
        }

        private static void AppendVector(StringBuilder report, Material material, string property)
        {
            if (material.HasProperty(property))
                report.AppendLine($"- `{property}`: {Format(material.GetVector(property))}");
        }

        private static void AppendColor(StringBuilder report, Material material, string property)
        {
            if (material.HasProperty(property))
                report.AppendLine($"- `{property}`: {Format(material.GetColor(property))}");
        }

        private static void AppendTexture(StringBuilder report, Material material, string property)
        {
            if (!material.HasProperty(property))
                return;

            Texture texture = material.GetTexture(property);
            string assetPath = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : "None";
            report.AppendLine($"- `{property}`: `{assetPath}`");
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string Format(Vector4 value)
        {
            return
                $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###}, {value.w:0.###})";
        }

        private static string GetOutputFolder()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(), "Artifacts", "RealisticWater", "R0");
        }

        private enum BenchmarkMode
        {
            Standard = 0,
            UnderwaterFallback = 1,
            UnderwaterSurface = 2,
        }

        private readonly struct BaselineView
        {
            public BaselineView(string fileName, Vector3 position, Vector3 target)
            {
                FileName = fileName;
                Position = position;
                Target = target;
            }

            public string FileName { get; }
            public Vector3 Position { get; }
            public Vector3 Target { get; }
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
            public bool Available => Count > 0;

            public string ToReportLine(string label)
            {
                if (!Available)
                    return $"- {label}: unavailable in this Editor session.";

                return
                    $"- {label}: avg {Average:0.00} ms, p95 {P95:0.00} ms, " +
                    $"max {Maximum:0.00} ms, over 16.67 ms {OverBudget}/{Count}.";
            }
        }

        private readonly struct PerformanceResult
        {
            public PerformanceResult(SampleStats frame, SampleStats cpu, SampleStats gpu)
            {
                Frame = frame;
                Cpu = cpu;
                Gpu = gpu;
            }

            public SampleStats Frame { get; }
            public SampleStats Cpu { get; }
            public SampleStats Gpu { get; }

            public void AppendTo(StringBuilder report)
            {
                report.AppendLine(Frame.ToReportLine("Observed frame time"));
                report.AppendLine(Cpu.ToReportLine("FrameTimingManager CPU"));
                report.AppendLine(Gpu.ToReportLine("FrameTimingManager GPU"));
                report.AppendLine();
                report.AppendLine(
                    "Editor Play Mode timings are comparative diagnostics, not a standalone-build " +
                    "performance guarantee.");
            }

            public string ToLogLine()
            {
                return
                    "RealisticWaterBaseline: " +
                    Frame.ToReportLine("frame") + " " +
                    Cpu.ToReportLine("CPU") + " " +
                    Gpu.ToReportLine("GPU");
            }
        }
    }
}
