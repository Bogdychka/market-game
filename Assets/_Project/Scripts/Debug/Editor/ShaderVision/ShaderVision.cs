using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Gives an off-Editor agent eyes for shader work. A JSON job in
    /// <c>Artifacts/ShaderVision/job.json</c> describes where to stand, what to freeze and what to
    /// vary; running it produces a labelled contact sheet, per-shot PNGs, measured statistics and
    /// an optional pixel diff against an earlier run, all under
    /// <c>Artifacts/ShaderVision/&lt;outputName&gt;/</c>.
    ///
    /// The three things that make captures usable as evidence:
    /// fixed camera poses, a frozen shader clock (<c>_Time</c> is overridden, so waves and scrolls
    /// have the same phase every run), and a fixed sun - otherwise every "before/after" diff is
    /// dominated by noise instead of the change under test.
    /// Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public static class ShaderVision
    {
        private const string RootFolder = "Artifacts/ShaderVision";
        private const int MaxShots = 24;

        private static ShaderVisionTimePass[] _timePasses;
        private static Camera _rigCamera;

        [MenuItem("Market/Debug/Shader Vision/Run Job")]
        public static void RunJob()
        {
            string jobPath = Path.Combine(ProjectRoot, RootFolder, "job.json");
            if (!File.Exists(jobPath))
            {
                Debug.LogError($"[ShaderVision] No job at {jobPath}. Write one first (see .claude/shader-vision/).");
                return;
            }

            ShaderVisionJob job;
            try
            {
                job = JsonUtility.FromJson<ShaderVisionJob>(File.ReadAllText(jobPath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ShaderVision] Could not parse {jobPath}: {exception.Message}");
                return;
            }

            if (job == null)
            {
                Debug.LogError($"[ShaderVision] Empty job at {jobPath}.");
                return;
            }

            Run(job);
        }

        /// <summary>
        /// Zero-setup capture of whatever the Scene view is currently looking at, measured the same
        /// way a job shot is. Useful when the user says "look at this" and there is no job yet.
        /// </summary>
        [MenuItem("Market/Debug/Shader Vision/Capture Scene View")]
        public static void CaptureSceneView()
        {
            Run(new ShaderVisionJob
            {
                outputName = "sceneview",
                useSceneViewCamera = true,
                width = 1280,
                height = 720,
                columns = 1,
            });
        }

        public static void Run(ShaderVisionJob job)
        {
            var report = new ShaderVisionReport
            {
                runId = job.runId,
                outputName = string.IsNullOrEmpty(job.outputName) ? "run" : job.outputName,
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };

            var warnings = new List<string>();
            string runDirectory = Path.Combine(ProjectRoot, RootFolder, report.outputName);

            try
            {
                Execute(job, report, warnings, runDirectory);
            }
            catch (Exception exception)
            {
                report.status = "error";
                report.error = exception.Message;
                Debug.LogError($"[ShaderVision] {exception}");
            }

            report.warnings = warnings.ToArray();
            Directory.CreateDirectory(runDirectory);
            File.WriteAllText(Path.Combine(runDirectory, "report.json"), JsonUtility.ToJson(report, true));

            Debug.Log(report.status == "ok"
                ? $"[ShaderVision] {report.outputName}: {report.shots.Length} shot(s) -> {RootFolder}/{report.outputName}/"
                : $"[ShaderVision] {report.outputName} FAILED: {report.error}");
        }

        private static void Execute(
            ShaderVisionJob job,
            ShaderVisionReport report,
            List<string> warnings,
            string runDirectory)
        {
            OpenSceneIfRequested(job, warnings);

            Scene scene = SceneManager.GetActiveScene();
            report.scene = scene.path;
            bool sceneWasClean = !scene.isDirty;

            var restores = new List<Action>();
            var frames = new List<ShaderVisionFrame>();

            try
            {
                ApplySun(job, restores, warnings);
                ApplyOverrides(job, restores, warnings);

                Camera camera = CreateRigCamera(out GameObject rig);
                restores.Add(() => UnityEngine.Object.DestroyImmediate(rig));

                if (job.freezeTime)
                    EnableTimeFreeze(camera, restores);

                List<ShaderVisionView> views = BuildViews(job, warnings);
                if (views.Count == 0)
                {
                    warnings.Add("No usable view; falling back to the Scene view camera pose.");
                    ShaderVisionView fallback = SceneViewPose();
                    if (fallback == null)
                        throw new InvalidOperationException("No views in the job and no Scene view camera available.");
                    views.Add(fallback);
                }

                if (job.sweep != null && !string.IsNullOrEmpty(job.sweep.property))
                    CaptureSweep(job, camera, views, frames, warnings);
                else
                    CaptureViews(job, camera, views, frames);
            }
            finally
            {
                for (int i = restores.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        restores[i]();
                    }
                    catch (Exception exception)
                    {
                        warnings.Add($"Restore step failed: {exception.Message}");
                    }
                }

                // The rig camera, the sun rotation and their undo all touch the scene. The health
                // gate requires 0 dirty scenes, so a capture must never leave one behind.
                if (sceneWasClean && scene.IsValid() && scene.isDirty)
                    TryClearSceneDirtiness(scene, warnings);
            }

            WriteResults(job, report, frames, runDirectory);
        }

        private static void WriteResults(
            ShaderVisionJob job,
            ShaderVisionReport report,
            List<ShaderVisionFrame> frames,
            string runDirectory)
        {
            Directory.CreateDirectory(runDirectory);

            string compareDirectory = string.IsNullOrEmpty(job.compareRun)
                ? null
                : Path.Combine(ProjectRoot, RootFolder, job.compareRun);

            var shotReports = new List<ShaderVisionShotReport>(frames.Count);
            foreach (ShaderVisionFrame frame in frames)
            {
                ShaderVisionShotReport shot = frame.Analyze();
                string fileName = $"shot_{SafeName(frame.Label)}.png";
                shot.file = fileName;

                // Diff BEFORE overwriting: when compareRun is this same run, the baseline on disk
                // is the previous capture of the very file we are about to replace.
                if (compareDirectory != null)
                {
                    string baseline = Path.Combine(compareDirectory, fileName);
                    ShaderVisionSheet.WriteDiff(
                        baseline,
                        frame,
                        Path.Combine(runDirectory, $"diff_{SafeName(frame.Label)}.png"),
                        shot);
                }

                ShaderVisionSheet.WritePng(Path.Combine(runDirectory, fileName), frame.Width, frame.Height, frame.Pixels);
                shotReports.Add(shot);
            }

            report.shots = shotReports.ToArray();

            if (frames.Count > 0)
            {
                string sheetPath = Path.Combine(runDirectory, "sheet.png");
                ShaderVisionSheet.WriteContactSheet(sheetPath, frames, job.columns);
                report.sheet = $"{RootFolder}/{report.outputName}/sheet.png";
            }
        }

        private static void CaptureViews(
            ShaderVisionJob job,
            Camera camera,
            List<ShaderVisionView> views,
            List<ShaderVisionFrame> frames)
        {
            int samples = Mathf.Max(1, job.timeSamples);
            foreach (ShaderVisionView view in views)
            {
                PlaceCamera(camera, view);
                for (int sample = 0; sample < samples; sample++)
                {
                    float time = job.time + sample * job.timeStep;
                    ApplyShaderTime(time);
                    string label = samples > 1
                        ? $"{view.name} T{time.ToString("0.##", CultureInfo.InvariantCulture)}"
                        : view.name;

                    frames.Add(ShaderVisionFrame.Capture(camera, label, job.width, job.height));
                    if (frames.Count >= MaxShots)
                        return;
                }
            }
        }

        private static void CaptureSweep(
            ShaderVisionJob job,
            Camera camera,
            List<ShaderVisionView> views,
            List<ShaderVisionFrame> frames,
            List<string> warnings)
        {
            ShaderVisionSweep sweep = job.sweep;
            Material material = LoadMaterial(sweep.material ?? job.material, warnings);
            if (material == null)
                return;

            int propertyId = Shader.PropertyToID(sweep.property);
            if (!material.HasProperty(propertyId))
            {
                warnings.Add($"Sweep skipped: material '{material.name}' has no property '{sweep.property}'.");
                return;
            }

            ShaderVisionView view = views.Find(v => v.name == sweep.view) ?? views[0];
            PlaceCamera(camera, view);
            ApplyShaderTime(job.time);

            bool isVector = sweep.type == "color" || sweep.type == "vector";
            int count = isVector
                ? (sweep.vectorValues?.Length ?? 0) / 4
                : sweep.values?.Length ?? 0;
            if (count == 0)
            {
                warnings.Add("Sweep skipped: no values supplied.");
                return;
            }

            Vector4 originalVector = isVector ? material.GetVector(propertyId) : Vector4.zero;
            float originalFloat = isVector ? 0f : material.GetFloat(propertyId);

            try
            {
                for (int i = 0; i < count && frames.Count < MaxShots; i++)
                {
                    string label;
                    if (isVector)
                    {
                        var value = new Vector4(
                            sweep.vectorValues[i * 4],
                            sweep.vectorValues[i * 4 + 1],
                            sweep.vectorValues[i * 4 + 2],
                            sweep.vectorValues[i * 4 + 3]);
                        material.SetVector(propertyId, value);
                        label = $"{TrimProperty(sweep.property)} {i}";
                    }
                    else
                    {
                        float value = sweep.values[i];
                        material.SetFloat(propertyId, value);
                        label = $"{TrimProperty(sweep.property)}={value.ToString("0.###", CultureInfo.InvariantCulture)}";
                    }

                    frames.Add(ShaderVisionFrame.Capture(camera, label, job.width, job.height));
                }
            }
            finally
            {
                if (isVector)
                    material.SetVector(propertyId, originalVector);
                else
                    material.SetFloat(propertyId, originalFloat);

                EditorUtility.ClearDirty(material);
            }
        }

        /// <summary>Sets the instant the next capture renders at; a no-op when the job lets time run.</summary>
        private static void ApplyShaderTime(float time)
        {
            if (_timePasses == null)
                return;

            foreach (ShaderVisionTimePass pass in _timePasses)
                pass.ShaderTime = time;
        }

        /// <summary>
        /// Hooks the frozen-clock pass onto the rig camera only, so nothing else in the Editor
        /// renders with a pinned time.
        /// </summary>
        private static void EnableTimeFreeze(Camera camera, List<Action> restores)
        {
            _timePasses = new[]
            {
                new ShaderVisionTimePass(RenderPassEvent.BeforeRenderingShadows),
                new ShaderVisionTimePass(RenderPassEvent.BeforeRenderingPrePasses),
                new ShaderVisionTimePass(RenderPassEvent.BeforeRenderingOpaques),
            };

            _rigCamera = camera;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            restores.Add(() =>
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                _timePasses = null;
                _rigCamera = null;
            });
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_timePasses == null || camera != _rigCamera)
                return;

            ScriptableRenderer renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            foreach (ShaderVisionTimePass pass in _timePasses)
                renderer.EnqueuePass(pass);
        }

        private static void OpenSceneIfRequested(ShaderVisionJob job, List<string> warnings)
        {
            if (string.IsNullOrEmpty(job.scene))
                return;

            Scene active = SceneManager.GetActiveScene();
            if (active.path == job.scene)
                return;

            if (active.isDirty)
                throw new InvalidOperationException(
                    $"Active scene '{active.name}' has unsaved changes; refusing to open '{job.scene}' over it.");

            if (!File.Exists(Path.Combine(ProjectRoot, job.scene)))
            {
                warnings.Add($"Scene '{job.scene}' not found; capturing the active scene instead.");
                return;
            }

            EditorSceneManager.OpenScene(job.scene, OpenSceneMode.Single);
        }

        private static void ApplySun(ShaderVisionJob job, List<Action> restores, List<string> warnings)
        {
            if (job.sun == null || !job.sun.apply)
                return;

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (light.type == LightType.Directional && light.isActiveAndEnabled)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            if (sun == null)
            {
                warnings.Add("Sun override skipped: no directional light in the scene.");
                return;
            }

            Quaternion originalRotation = sun.transform.rotation;
            float originalIntensity = sun.intensity;
            restores.Add(() =>
            {
                sun.transform.rotation = originalRotation;
                sun.intensity = originalIntensity;
            });

            sun.transform.rotation = Quaternion.Euler(job.sun.pitch, job.sun.yaw, 0f);
            if (job.sun.intensity >= 0f)
                sun.intensity = job.sun.intensity;
        }

        private static void ApplyOverrides(ShaderVisionJob job, List<Action> restores, List<string> warnings)
        {
            if (job.overrides == null)
                return;

            foreach (ShaderVisionOverride entry in job.overrides)
            {
                if (entry == null || string.IsNullOrEmpty(entry.property))
                    continue;

                Material material = LoadMaterial(string.IsNullOrEmpty(entry.material) ? job.material : entry.material, warnings);
                if (material == null)
                    continue;

                int propertyId = Shader.PropertyToID(entry.property);
                if (!material.HasProperty(propertyId))
                {
                    warnings.Add($"Override skipped: '{material.name}' has no property '{entry.property}'.");
                    continue;
                }

                if (entry.type == "color" || entry.type == "vector")
                {
                    if (entry.vector == null || entry.vector.Length < 4)
                    {
                        warnings.Add($"Override skipped: '{entry.property}' needs 4 vector components.");
                        continue;
                    }

                    Vector4 original = material.GetVector(propertyId);
                    Material captured = material;
                    restores.Add(() =>
                    {
                        captured.SetVector(propertyId, original);
                        EditorUtility.ClearDirty(captured);
                    });
                    material.SetVector(propertyId, new Vector4(entry.vector[0], entry.vector[1], entry.vector[2], entry.vector[3]));
                }
                else
                {
                    float original = material.GetFloat(propertyId);
                    Material captured = material;
                    restores.Add(() =>
                    {
                        captured.SetFloat(propertyId, original);
                        EditorUtility.ClearDirty(captured);
                    });
                    material.SetFloat(propertyId, entry.value);
                }
            }
        }

        private static List<ShaderVisionView> BuildViews(ShaderVisionJob job, List<string> warnings)
        {
            var views = new List<ShaderVisionView>();

            if (job.useSceneViewCamera)
            {
                ShaderVisionView pose = SceneViewPose();
                if (pose != null)
                    views.Add(pose);
                else
                    warnings.Add("Scene view camera requested but no Scene view is open.");
            }

            if (job.views != null)
            {
                foreach (ShaderVisionView view in job.views)
                {
                    if (view != null && view.position != null && view.position.Length >= 3)
                        views.Add(view);
                }
            }

            if (job.turntable != null && !string.IsNullOrEmpty(job.turntable.target))
                views.AddRange(BuildTurntable(job.turntable, warnings));

            return views;
        }

        private static IEnumerable<ShaderVisionView> BuildTurntable(ShaderVisionTurntable turntable, List<string> warnings)
        {
            var views = new List<ShaderVisionView>();
            GameObject target = GameObject.Find(turntable.target);
            if (target == null)
            {
                warnings.Add($"Turntable skipped: no active GameObject named '{turntable.target}'.");
                return views;
            }

            Bounds bounds = MeasureBounds(target);
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            float distance = radius * Mathf.Max(0.2f, turntable.distanceScale);
            int angles = Mathf.Clamp(turntable.angles, 1, 12);

            for (int i = 0; i < angles; i++)
            {
                float yaw = turntable.startYaw + 360f * i / angles;
                Vector3 direction = Quaternion.Euler(turntable.elevation, yaw, 0f) * Vector3.forward;
                Vector3 position = bounds.center - direction * distance;
                views.Add(new ShaderVisionView
                {
                    name = $"{turntable.target} {Mathf.RoundToInt(yaw)}",
                    position = new[] { position.x, position.y, position.z },
                    lookAt = new[] { bounds.center.x, bounds.center.y, bounds.center.z },
                    fov = turntable.fov,
                });
            }

            return views;
        }

        private static Bounds MeasureBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one * 4f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static ShaderVisionView SceneViewPose()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
                return null;

            Transform t = view.camera.transform;
            Vector3 euler = t.rotation.eulerAngles;
            return new ShaderVisionView
            {
                name = "sceneview",
                position = new[] { t.position.x, t.position.y, t.position.z },
                euler = new[] { euler.x, euler.y, euler.z },
                fov = view.camera.fieldOfView,
                near = view.camera.nearClipPlane,
                far = view.camera.farClipPlane,
            };
        }

        /// <summary>
        /// A throwaway camera with post processing on: the project tonemaps in post, so a raw
        /// camera would capture an untonemapped frame that looks nothing like the game.
        /// </summary>
        private static Camera CreateRigCamera(out GameObject rig)
        {
            rig = new GameObject("ShaderVisionRig") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = rig.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = ~0;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.enabled = false;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return camera;
        }

        private static void PlaceCamera(Camera camera, ShaderVisionView view)
        {
            var position = new Vector3(view.position[0], view.position[1], view.position[2]);
            camera.transform.position = position;

            if (view.lookAt != null && view.lookAt.Length >= 3)
            {
                var target = new Vector3(view.lookAt[0], view.lookAt[1], view.lookAt[2]);
                Vector3 direction = target - position;
                if (direction.sqrMagnitude > 1e-6f)
                    camera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            else if (view.euler != null && view.euler.Length >= 3)
            {
                camera.transform.rotation = Quaternion.Euler(view.euler[0], view.euler[1], view.euler[2]);
            }

            camera.fieldOfView = Mathf.Clamp(view.fov, 5f, 170f);
            camera.nearClipPlane = Mathf.Max(0.01f, view.near);
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 1f, view.far);
        }

        private static Material LoadMaterial(string assetPath, List<string> warnings)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                warnings.Add("Material path missing; set 'material' on the job or the entry.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
                warnings.Add($"Material not found at '{assetPath}'.");

            return material;
        }

        private static string TrimProperty(string property)
        {
            return string.IsNullOrEmpty(property) ? "P" : property.TrimStart('_');
        }

        private static string SafeName(string label)
        {
            var builder = new StringBuilder(label.Length);
            foreach (char c in label)
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');

            return builder.ToString();
        }

        /// <summary>
        /// Restores the scene's clean flag after a capture. The rig camera and the sun override are
        /// both undone, so the scene content is byte-identical - only the flag is stale, and Unity 6
        /// keeps the reset internal. Reflection with a graceful warning beats leaving a scene that
        /// the health gate will fail on.
        /// </summary>
        private static void TryClearSceneDirtiness(Scene scene, List<string> warnings)
        {
            System.Reflection.MethodInfo method = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

            if (method == null)
            {
                warnings.Add($"Scene '{scene.name}' is flagged dirty by the capture; its contents were restored, so discard the change.");
                return;
            }

            method.Invoke(null, new object[] { scene });
        }

        private static string ProjectRoot => Directory.GetCurrentDirectory();
    }
}
