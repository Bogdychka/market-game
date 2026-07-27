using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Market.DebugTools
{
    /// <summary>
    /// Captures the fixed R9 view and frame timings from a standalone development build.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealisticWaterStandaloneCapture : MonoBehaviour
    {
        private const string CaptureFlag = "-waterR9Capture";
        private const string OutputArgument = "-waterR9Output";
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const float WarmupSeconds = 2f;
        private const float MeasureSeconds = 8f;

        private static readonly Vector3 CameraPosition = new(32f, 24f, -34f);
        private static readonly Vector3 CameraTarget = new(0f, -2f, 7f);
        private static readonly FrameTiming[] FrameTimings = new FrameTiming[1];

        private readonly List<float> _frameSamples = new(1024);
        private readonly List<float> _cpuSamples = new(1024);
        private readonly List<float> _gpuSamples = new(1024);

        private IEnumerator Start()
        {
            if (Application.isEditor || !HasCaptureFlag())
                yield break;

            string outputFolder = ResolveOutputFolder();
            ConfigureCaptureView();
            Screen.SetResolution(CaptureWidth, CaptureHeight, false);
            yield return null;
            yield return null;
            yield return CaptureRun(outputFolder);
            Application.Quit(0);
        }

        private IEnumerator CaptureRun(string outputFolder)
        {
            double warmupEnd = Time.realtimeSinceStartupAsDouble + WarmupSeconds;
            while (Time.realtimeSinceStartupAsDouble < warmupEnd)
                yield return null;

            FrameTimingManager.CaptureFrameTimings();
            double sampleEnd = Time.realtimeSinceStartupAsDouble + MeasureSeconds;
            while (Time.realtimeSinceStartupAsDouble < sampleEnd)
            {
                CaptureFrameSample();
                yield return null;
            }

            string screenshotPath = Path.Combine(outputFolder, "standalone_high.png");
            if (!TryCreateDirectory(outputFolder) ||
                !TryCaptureScreenshot(screenshotPath))
            {
                yield break;
            }

            yield return WaitForScreenshot(screenshotPath);
            TryWriteReport(outputFolder);
        }

        private void ConfigureCaptureView()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            foreach (Behaviour behaviour in
                     camera.transform.root.GetComponentsInChildren<Behaviour>(true))
            {
                string typeName = behaviour.GetType().Name;
                if (typeName == "FirstPersonController" || typeName == "HeadBob")
                    behaviour.enabled = false;
            }

            camera.transform.position = CameraPosition;
            camera.transform.rotation = Quaternion.LookRotation(
                CameraTarget - CameraPosition, Vector3.up);
            camera.fieldOfView = 60f;
            GetComponent<RealisticWaterQualityController>()?.SetQuality(
                RealisticWaterQualityTier.High);
        }

        private void CaptureFrameSample()
        {
            _frameSamples.Add(Time.unscaledDeltaTime * 1000f);
            uint count = FrameTimingManager.GetLatestTimings(1, FrameTimings);
            FrameTimingManager.CaptureFrameTimings();
            if (count == 0)
                return;
            if (FrameTimings[0].cpuFrameTime > 0.0)
                _cpuSamples.Add((float)FrameTimings[0].cpuFrameTime);
            if (FrameTimings[0].gpuFrameTime > 0.0)
                _gpuSamples.Add((float)FrameTimings[0].gpuFrameTime);
        }

        private static IEnumerator WaitForScreenshot(string path)
        {
            double timeout = Time.realtimeSinceStartupAsDouble + 5.0;
            while (!File.Exists(path) &&
                   Time.realtimeSinceStartupAsDouble < timeout)
            {
                yield return null;
            }
        }

        private void TryWriteReport(string outputFolder)
        {
            try
            {
                var report = new StringBuilder(1024);
                report.AppendLine("# R9 Standalone Development Build Capture");
                report.AppendLine();
                report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                report.AppendLine($"Unity: {Application.unityVersion}");
                report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
                report.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
                report.AppendLine($"Resolution: {Screen.width}x{Screen.height}");
                report.AppendLine("Quality tier: High");
                report.AppendLine();
                report.AppendLine(FormatStats("Observed frame time", _frameSamples));
                report.AppendLine(FormatStats("FrameTimingManager CPU", _cpuSamples));
                report.AppendLine(FormatStats("FrameTimingManager GPU", _gpuSamples));
                File.WriteAllText(
                    Path.Combine(outputFolder, "standalone_performance.md"),
                    report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneCapture: report failed: {exception.Message}");
            }
        }

        private static string FormatStats(string label, List<float> samples)
        {
            if (samples.Count == 0)
                return $"- {label}: unavailable.";

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
            return
                $"- {label}: avg {total / samples.Count:0.00} ms, " +
                $"p95 {samples[p95Index]:0.00} ms, " +
                $"max {samples[samples.Count - 1]:0.00} ms, " +
                $"over 16.67 ms {overBudget}/{samples.Count}.";
        }

        private static bool HasCaptureFlag()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == CaptureFlag)
                    return true;
            }

            return false;
        }

        private static string ResolveOutputFolder()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == OutputArgument)
                {
                    try
                    {
                        return Path.GetFullPath(arguments[i + 1]);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"RealisticWaterStandaloneCapture: output path failed: " +
                            exception.Message);
                    }
                }
            }

            return Path.Combine(
                Application.persistentDataPath,
                "RealisticWaterR9");
        }

        private static bool TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneCapture: directory failed: {exception.Message}");
                return false;
            }
        }

        private static bool TryCaptureScreenshot(string path)
        {
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RealisticWaterStandaloneCapture: screenshot failed: {exception.Message}");
                return false;
            }
        }
    }
}
