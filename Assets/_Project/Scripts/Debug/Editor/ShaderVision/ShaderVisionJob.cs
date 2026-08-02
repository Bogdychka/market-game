using System;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// A single camera pose. <c>lookAt</c> wins over <c>euler</c> when both are present.
    /// Poses live in the job file so a "before" and an "after" run frame exactly the same pixels.
    /// </summary>
    [Serializable]
    public class ShaderVisionView
    {
        public string name = "view";
        public float[] position = { 0f, 2f, -6f };
        public float[] lookAt;
        public float[] euler;
        public float fov = 60f;
        public float near = 0.05f;
        public float far = 3000f;
    }

    /// <summary>Orbit a named GameObject: N evenly spaced yaw steps around its renderer bounds.</summary>
    [Serializable]
    public class ShaderVisionTurntable
    {
        public string target;
        public int angles = 4;
        public float startYaw;
        public float elevation = 22f;
        public float distanceScale = 1.8f;
        public float fov = 55f;
    }

    /// <summary>One material property write applied before every shot of the run.</summary>
    [Serializable]
    public class ShaderVisionOverride
    {
        public string material;
        public string property;
        public string type = "float";
        public float value;
        public float[] vector;
    }

    /// <summary>Renders the same pose once per value so one image answers "which setting looks right".</summary>
    [Serializable]
    public class ShaderVisionSweep
    {
        public string material;
        public string property;
        public string type = "float";
        public float[] values;
        /// <summary>Flat RGBA groups (stride 4) used when <c>type</c> is "color" or "vector".</summary>
        public float[] vectorValues;
        public string view;
    }

    /// <summary>Deterministic key light, so lighting is never the reason two runs differ.</summary>
    [Serializable]
    public class ShaderVisionSun
    {
        public bool apply;
        public float yaw = -140f;
        public float pitch = 42f;
        public float intensity = -1f;
    }

    /// <summary>
    /// Capture job read from <c>Artifacts/ShaderVision/job.json</c>. Everything an agent needs to
    /// look at a shader is here: where to stand, what to freeze, what to vary, what to compare to.
    /// </summary>
    [Serializable]
    public class ShaderVisionJob
    {
        public string runId = "";
        public string outputName = "run";
        public string scene;
        public int width = 512;
        public int height = 288;
        public int columns = 3;

        /// <summary>Pins the shader clock so waves/scrolls have the same phase in every run.</summary>
        public bool freezeTime = true;
        public float time = 8f;
        public int timeSamples = 1;
        public float timeStep = 0.35f;

        public bool useSceneViewCamera;
        public ShaderVisionSun sun = new ShaderVisionSun();
        public ShaderVisionView[] views = Array.Empty<ShaderVisionView>();
        public ShaderVisionTurntable turntable;

        public string material;
        public ShaderVisionOverride[] overrides = Array.Empty<ShaderVisionOverride>();
        public ShaderVisionSweep sweep;

        /// <summary>outputName of an earlier run; matching shots get a numeric diff and a heatmap.</summary>
        public string compareRun;
    }

    /// <summary>Per-shot numbers. Cheap to read, and they catch what eyeballing a PNG misses.</summary>
    [Serializable]
    public class ShaderVisionShotReport
    {
        public string label;
        public string file;
        public float luminanceMean;
        public float luminanceMin;
        public float luminanceMax;
        public float luminanceStdDev;
        public float luminanceP05;
        public float luminanceP50;
        public float luminanceP95;
        public float[] rgbMean;
        public float blackPct;
        public float clippedPct;
        public float nonFinitePct;
        public float magentaPct;
        public float detail;

        public bool compared;
        public string diffFile;
        public float meanAbsDiff;
        public float maxAbsDiff;
        public float changedPct;
    }

    [Serializable]
    public class ShaderVisionReport
    {
        public string status = "ok";
        public string error;
        public string runId;
        public string outputName;
        public string scene;
        public string sheet;
        public string generatedAtUtc;
        public string[] warnings = Array.Empty<string>();
        public ShaderVisionShotReport[] shots = Array.Empty<ShaderVisionShotReport>();
    }
}
