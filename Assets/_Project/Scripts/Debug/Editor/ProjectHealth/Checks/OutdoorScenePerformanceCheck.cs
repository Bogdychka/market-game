using System;
using System.IO;

namespace Market.DebugTools.Editor.Checks
{
    /// <summary>Finds outdoor-scene render settings that caused the Island GPU bottleneck.</summary>
    public sealed class OutdoorScenePerformanceCheck : IProjectHealthCheck
    {
        private const string WaterShaderPath =
            "Assets/_Project/Art/Materials/Water/MarketWater.shader";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        public string Name => "Outdoor scene performance";
        public ProjectHealthCategory Category => ProjectHealthCategory.Performance;

        public void Scan(ProjectHealthContext context, ProjectHealthReport report)
        {
            string[] scenes = context.FindAssetPaths("t:Scene");
            for (int i = 0; i < scenes.Length; i++)
                ScanScene(context, scenes[i], report);

            ScanGlobalRenderSetting(context, report);
            ScanWaterShader(context, report);
        }

        private static void ScanScene(
            ProjectHealthContext context,
            string path,
            ProjectHealthReport report)
        {
            string yaml = ReadText(path, report);
            if (yaml == null || !yaml.Contains("Terrain:", StringComparison.Ordinal))
                return;

            context.Track(path);
            AddTerrainIssue(yaml, path, report, "m_DrawInstanced", "0",
                "Terrain instancing disabled", "Enable Draw Instanced on outdoor Terrain components.");
            AddTerrainIssue(yaml, path, report, "m_ShadowCastingMode", "2",
                "Terrain uses two-sided shadows", "Use one-sided or disabled Terrain shadows.");
            if (ProjectHealthRules.SerializedComponentFloatBelow(
                    yaml, "Terrain", "m_HeightmapPixelError", 10f))
            {
                report.Add(new ProjectHealthIssue(
                    ProjectHealthSeverity.Error,
                    ProjectHealthCategory.Performance,
                    "Terrain LOD is too aggressive",
                    "Keep heightmap pixel error at 10 or higher unless profiling proves GPU headroom.",
                    path));
            }
        }

        private static void AddTerrainIssue(
            string yaml,
            string path,
            ProjectHealthReport report,
            string setting,
            string value,
            string title,
            string description)
        {
            if (!ProjectHealthRules.SerializedComponentHasSetting(yaml, "Terrain", setting, value))
                return;

            report.Add(new ProjectHealthIssue(
                ProjectHealthSeverity.Error,
                ProjectHealthCategory.Performance,
                title,
                description,
                path));
        }

        private static void ScanGlobalRenderSetting(
            ProjectHealthContext context,
            ProjectHealthReport report)
        {
            context.Track(PcPipelinePath);
            string text = ReadText(PcPipelinePath, report);
            if (text == null || !text.Contains("m_RequireOpaqueTexture: 1", StringComparison.Ordinal))
                return;

            report.Add(new ProjectHealthIssue(
                ProjectHealthSeverity.Error,
                ProjectHealthCategory.Performance,
                "Global opaque texture enabled",
                "Keep the URP opaque copy off globally; enable it only per camera for a measured effect.",
                PcPipelinePath));
        }

        private static void ScanWaterShader(
            ProjectHealthContext context,
            ProjectHealthReport report)
        {
            context.Track(WaterShaderPath);
            string text = ReadText(WaterShaderPath, report);
            if (text == null)
                return;

            if (text.Contains("DeclareOpaqueTexture.hlsl", StringComparison.Ordinal)
                || text.Contains("SampleSceneColor", StringComparison.Ordinal)
                || text.Contains("Cull Off", StringComparison.Ordinal))
            {
                report.Add(new ProjectHealthIssue(
                    ProjectHealthSeverity.Error,
                    ProjectHealthCategory.Performance,
                    "Expensive ocean shader path",
                    "The ocean must avoid opaque-color copies and render only front faces.",
                    WaterShaderPath));
            }
        }

        private static string ReadText(string path, ProjectHealthReport report)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException exception)
            {
                report.Add(new ProjectHealthIssue(
                    ProjectHealthSeverity.Warning,
                    ProjectHealthCategory.Performance,
                    "Could not inspect render settings",
                    exception.Message,
                    path));
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                report.Add(new ProjectHealthIssue(
                    ProjectHealthSeverity.Warning,
                    ProjectHealthCategory.Performance,
                    "Could not inspect render settings",
                    exception.Message,
                    path));
                return null;
            }
        }
    }
}
