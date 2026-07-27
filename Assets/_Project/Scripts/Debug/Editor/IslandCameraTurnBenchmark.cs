using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>Measures frame-time spikes during a repeatable Island camera rotation.</summary>
    [InitializeOnLoad]
    public static class IslandCameraTurnBenchmark
    {
        private const string PendingKey = "Market.IslandCameraTurnBenchmark.Pending";
        private const float WarmupSeconds = 1.5f;
        private const float MeasureSeconds = 6f;
        private const float TurnDegreesPerSecond = 60f;

        private static readonly List<float> Samples = new(512);
        private static Transform _playerRoot;
        private static SceneView _sceneView;
        private static float _startedAt;
        private static double _sceneStartedAt;
        private static double _lastSceneUpdate;
        private static int _lastFrame;

        static IslandCameraTurnBenchmark()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Market/Debug/Benchmark Island Camera Turn")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            if (Application.isPlaying)
                BeginSampling();
            else
                EditorApplication.isPlaying = true;
        }

        [MenuItem("Market/Debug/Benchmark Island Scene View Turn")]
        public static void RunSceneView()
        {
            if (Application.isPlaying || SceneView.lastActiveSceneView == null)
            {
                Debug.LogError("Island Scene View benchmark requires Edit Mode and an open Scene View.");
                return;
            }

            Samples.Clear();
            _sceneView = SceneView.lastActiveSceneView;
            _sceneStartedAt = EditorApplication.timeSinceStartup;
            _lastSceneUpdate = _sceneStartedAt;
            EditorApplication.update -= TickSceneView;
            EditorApplication.update += TickSceneView;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                BeginSampling();
            else if (state == PlayModeStateChange.ExitingPlayMode)
                StopSampling();
        }

        private static void BeginSampling()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("Island camera turn benchmark: Main Camera not found.");
                Finish();
                return;
            }

            Samples.Clear();
            _playerRoot = camera.transform.root;
            _startedAt = Time.realtimeSinceStartup;
            _lastFrame = -1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying || _playerRoot == null || Time.frameCount == _lastFrame)
                return;

            _lastFrame = Time.frameCount;
            float elapsed = Time.realtimeSinceStartup - _startedAt;
            if (elapsed < WarmupSeconds)
                return;
            if (elapsed >= WarmupSeconds + MeasureSeconds)
            {
                Report("Island camera turn benchmark");
                Finish();
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            _playerRoot.Rotate(0f, TurnDegreesPerSecond * deltaTime, 0f, Space.World);
            Samples.Add(deltaTime * 1000f);
        }

        private static void TickSceneView()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - _lastSceneUpdate);
            _lastSceneUpdate = now;
            float elapsed = (float)(now - _sceneStartedAt);
            if (elapsed >= WarmupSeconds + MeasureSeconds)
            {
                Report("Island Scene View turn benchmark");
                EditorApplication.update -= TickSceneView;
                _sceneView = null;
                return;
            }

            if (elapsed < WarmupSeconds || _sceneView == null)
                return;

            _sceneView.rotation = Quaternion.AngleAxis(
                TurnDegreesPerSecond * deltaTime, Vector3.up) * _sceneView.rotation;
            _sceneView.Repaint();
            Samples.Add(deltaTime * 1000f);
        }

        private static void Report(string label)
        {
            Samples.Sort();
            float total = 0f;
            int overBudget = 0;
            for (int i = 0; i < Samples.Count; i++)
            {
                total += Samples[i];
                if (Samples[i] > 16.67f)
                    overBudget++;
            }

            int p95Index = Mathf.Clamp(Mathf.CeilToInt(Samples.Count * 0.95f) - 1, 0, Samples.Count - 1);
            float average = Samples.Count > 0 ? total / Samples.Count : 0f;
            float p95 = Samples.Count > 0 ? Samples[p95Index] : 0f;
            float maximum = Samples.Count > 0 ? Samples[Samples.Count - 1] : 0f;
            Debug.Log($"{label}: avg={average:0.00} ms, p95={p95:0.00} ms, " +
                      $"max={maximum:0.00} ms, over16.67={overBudget}/{Samples.Count}.");
        }

        private static void Finish()
        {
            StopSampling();
            SessionState.SetBool(PendingKey, false);
            if (Application.isPlaying)
                EditorApplication.isPlaying = false;
        }

        private static void StopSampling()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update -= TickSceneView;
            _playerRoot = null;
            _sceneView = null;
        }
    }
}
