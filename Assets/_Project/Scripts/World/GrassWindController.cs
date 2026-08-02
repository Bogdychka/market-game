using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// The one wind in the scene. Pushes a single direction, speed and gust into the shader globals
    /// <c>GrassWind.shader</c> reads, so every plant answers the same weather instead of each
    /// material carrying its own private breeze - a material only decides how hard it responds
    /// (<c>_WindResponse</c>). This is how wind is normally modelled: one global field per world
    /// (Unity WindZone, Unreal's Wind Directional Source), never a per-surface constant.
    /// One instance per scene that contains GrassWind-shaded grass; the shader falls back to a
    /// default breeze if a scene forgets one, so grass is never frozen solid.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Market/World/Grass Wind Controller")]
    public class GrassWindController : MonoBehaviour
    {
        private static readonly int DirectionId = Shader.PropertyToID("_GrassWindDirection");
        private static readonly int MotionId = Shader.PropertyToID("_GrassWindMotion");
        private static readonly int SquashId = Shader.PropertyToID("_GrassWindSquash");

        [Header("Direction")]
        [Tooltip("Compass heading the wind blows toward, in degrees around Y.")]
        [SerializeField, Range(0f, 360f)] private float _headingDegrees = 45f;

        [Header("Sway")]
        [Tooltip("How fast the sway wave beats.")]
        [SerializeField, Range(0f, 6f)] private float _swaySpeed = 1.6f;
        [Tooltip("How tightly the wave repeats across the ground. Low = whole field moves together.")]
        [SerializeField, Range(0.05f, 4f)] private float _swayFrequency = 1.2f;
        [Tooltip("How far a blade tip travels, in metres.")]
        [SerializeField, Range(0f, 0.5f)] private float _swayStrength = 0.05f;

        [Header("Gusts")]
        [Tooltip("How much the strength swells and drops. 0 = a metronome, which reads as fake.")]
        [SerializeField, Range(0f, 1f)] private float _gustDepth = 0.45f;
        [Tooltip("How often gusts roll through, in cycles per second.")]
        [SerializeField, Range(0.01f, 2f)] private float _gustSpeed = 0.22f;

        [Header("Jelly")]
        [SerializeField, Range(0f, 8f)] private float _wobbleSpeed = 2.4f;
        [SerializeField, Range(0.05f, 4f)] private float _wobbleFrequency = 0.8f;
        [SerializeField, Range(0f, 0.3f)] private float _wobbleAmount = 0.03f;
        [SerializeField, Range(0f, 1f)] private float _squashAmount = 0.15f;

        /// <summary>Heading the wind blows toward, in degrees around Y.</summary>
        public float HeadingDegrees
        {
            get => _headingDegrees;
            set { _headingDegrees = value; Apply(); }
        }

        /// <summary>Sway distance in metres, before gusting.</summary>
        public float SwayStrength
        {
            get => _swayStrength;
            set { _swayStrength = Mathf.Max(0f, value); Apply(); }
        }

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void LateUpdate() => Apply();

        private void Apply()
        {
            float heading = _headingDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));

            // Perlin rather than a sine so gusts do not settle into an obvious period; realtime
            // rather than Time.time so the field keeps breathing in the editor, where the component
            // runs under ExecuteAlways but the game clock does not advance.
            float noise = Mathf.PerlinNoise(Time.realtimeSinceStartup * _gustSpeed, 0.37f);
            float gust = 1f + (noise * 2f - 1f) * _gustDepth;

            Shader.SetGlobalVector(
                DirectionId,
                new Vector4(direction.x, direction.y, _swaySpeed, Mathf.Max(_swayFrequency, 0.001f)));
            Shader.SetGlobalVector(
                MotionId,
                new Vector4(_swayStrength * gust, _wobbleSpeed, _wobbleFrequency, _wobbleAmount * gust));
            Shader.SetGlobalFloat(SquashId, _squashAmount);
        }
    }
}
