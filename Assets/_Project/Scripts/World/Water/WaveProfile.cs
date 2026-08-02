using System;
using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// How a wave layer travels across the surface.
    /// </summary>
    public enum WaveLayerMode
    {
        /// <summary>Parallel crests travelling along one direction (open water, swell).</summary>
        Directional = 0,

        /// <summary>Rings radiating out of a world-space origin (pond, spring, lake).</summary>
        Circular = 1,
    }

    /// <summary>
    /// One Gerstner layer of a <see cref="WaveProfile"/>.
    /// </summary>
    [Serializable]
    public sealed class WaveLayer
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private WaveLayerMode _mode = WaveLayerMode.Directional;

        [Tooltip("Crest-to-crest distance in metres. Drives the wave speed too: " +
            "deep-water dispersion makes long waves travel faster.")]
        [SerializeField] private float _wavelength = 8f;

        [Tooltip("Vertical half-height of the layer in metres.")]
        [SerializeField] private float _amplitude = 0.2f;

        [Tooltip("0 = a round sine swell, 1 = a sharp peaked crest.")]
        [SerializeField] [Range(0f, 1f)] private float _steepness = 0.4f;

        [Tooltip("Travel direction in degrees, bent toward the wind by Wind Spread. " +
            "Directional layers only.")]
        [SerializeField] private float _directionAngle;

        [Tooltip("World XZ point the rings radiate from. Circular layers only.")]
        [SerializeField] private Vector2 _origin;

        [Tooltip("Scales the dispersion speed of this layer. 1 = physically derived.")]
        [SerializeField] private float _speedMultiplier = 1f;

        /// <summary>Whether this layer contributes to the surface.</summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Directional or circular travel.</summary>
        public WaveLayerMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>Crest-to-crest distance in metres.</summary>
        public float Wavelength
        {
            get => _wavelength;
            set => _wavelength = Mathf.Max(0.05f, value);
        }

        /// <summary>Vertical half-height in metres.</summary>
        public float Amplitude
        {
            get => _amplitude;
            set => _amplitude = Mathf.Max(0f, value);
        }

        /// <summary>Crest sharpness in the 0-1 range.</summary>
        public float Steepness
        {
            get => _steepness;
            set => _steepness = Mathf.Clamp01(value);
        }

        /// <summary>Travel direction in degrees for directional layers.</summary>
        public float DirectionAngle
        {
            get => _directionAngle;
            set => _directionAngle = value;
        }

        /// <summary>World XZ origin for circular layers.</summary>
        public Vector2 Origin
        {
            get => _origin;
            set => _origin = value;
        }

        /// <summary>Art-directed multiplier on the dispersion speed.</summary>
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Copies every authored value from another layer.</summary>
        public void CopyFrom(WaveLayer other)
        {
            if (other == null)
                return;

            _enabled = other._enabled;
            _mode = other._mode;
            _wavelength = other._wavelength;
            _amplitude = other._amplitude;
            _steepness = other._steepness;
            _directionAngle = other._directionAngle;
            _origin = other._origin;
            _speedMultiplier = other._speedMultiplier;
        }
    }

    /// <summary>
    /// A bank of Gerstner wave layers shared by the water shaders and by the C# sampler.
    /// Multipliers and per-index curves shape the whole bank without editing every layer, and the
    /// stored procedural settings let the wizard rebuild the exact same layers from a seed.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaveProfile",
        menuName = "Market/Water/Wave Profile",
        order = 200)]
    public sealed class WaveProfile : ScriptableObject
    {
        /// <summary>Hard limit shared with RealisticWaterWaves.hlsl.</summary>
        public const int MaxLayers = 8;

        [Header("Layers")]
        [SerializeField] private List<WaveLayer> _layers = new();

        [Header("Multipliers")]
        [Tooltip("Scales the wavelength of every layer.")]
        [SerializeField] private float _wavelengthMultiplier = 1f;

        [Tooltip("Scales the amplitude of every layer. The one knob for calm-to-storm.")]
        [SerializeField] private float _amplitudeMultiplier = 1f;

        [Tooltip("Scales the steepness of every layer.")]
        [SerializeField] private float _steepnessMultiplier = 1f;

        [Header("Curves (value over layer index)")]
        [Tooltip("Extra wavelength scale across the bank, first layer at 0, last at 1.")]
        [SerializeField] private AnimationCurve _wavelengthCurve = ConstantCurve();

        [Tooltip("Extra amplitude scale across the bank, first layer at 0, last at 1.")]
        [SerializeField] private AnimationCurve _amplitudeCurve = ConstantCurve();

        [Tooltip("Extra steepness scale across the bank, first layer at 0, last at 1.")]
        [SerializeField] private AnimationCurve _steepnessCurve = ConstantCurve();

        [Tooltip("How close to folding a crest may get. Lower rounds the crests off and " +
            "guarantees the surface stays a function of X and Z; 0.95 is the tuned default.")]
        [SerializeField] [Range(0.1f, 1f)] private float _steepnessClamping = 0.95f;

        [Header("Procedural")]
        [SerializeField] private WaveGenerationSettings _generation = new();

        /// <summary>Authored layers, including the disabled ones.</summary>
        public List<WaveLayer> Layers => _layers;

        /// <summary>Scales the wavelength of every layer.</summary>
        public float WavelengthMultiplier
        {
            get => _wavelengthMultiplier;
            set => _wavelengthMultiplier = Mathf.Max(0.001f, value);
        }

        /// <summary>Scales the amplitude of every layer.</summary>
        public float AmplitudeMultiplier
        {
            get => _amplitudeMultiplier;
            set => _amplitudeMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Scales the steepness of every layer.</summary>
        public float SteepnessMultiplier
        {
            get => _steepnessMultiplier;
            set => _steepnessMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Fold limit handed to the shader as _WaveFoldLimit.</summary>
        public float SteepnessClamping
        {
            get => _steepnessClamping;
            set => _steepnessClamping = Mathf.Clamp(value, 0.1f, 1f);
        }

        /// <summary>Seeded settings the procedural editor generates layers from.</summary>
        public WaveGenerationSettings Generation => _generation;

        /// <summary>Number of enabled layers, capped at <see cref="MaxLayers"/>.</summary>
        public int ActiveLayerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _layers.Count && count < MaxLayers; i++)
                {
                    if (_layers[i] != null && _layers[i].Enabled)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Fills <paramref name="destination"/> with the enabled layers after multipliers and
        /// curves are applied. This is what both the shader upload and the C# sampler read, so
        /// neither can drift from the authored asset. Does not allocate.
        /// </summary>
        public void ResolveLayers(List<ResolvedWaveLayer> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            int activeCount = ActiveLayerCount;
            if (activeCount == 0)
                return;

            int resolvedIndex = 0;
            for (int i = 0; i < _layers.Count && destination.Count < MaxLayers; i++)
            {
                WaveLayer layer = _layers[i];
                if (layer == null || !layer.Enabled)
                    continue;

                float t = activeCount > 1
                    ? resolvedIndex / (float)(activeCount - 1)
                    : 0f;
                resolvedIndex++;

                destination.Add(new ResolvedWaveLayer(
                    Mathf.Max(
                        0.05f,
                        layer.Wavelength * _wavelengthMultiplier *
                            EvaluateCurve(_wavelengthCurve, t)),
                    Mathf.Max(
                        0f,
                        layer.Amplitude * _amplitudeMultiplier *
                            EvaluateCurve(_amplitudeCurve, t)),
                    Mathf.Clamp01(
                        layer.Steepness * _steepnessMultiplier *
                            EvaluateCurve(_steepnessCurve, t)),
                    layer.DirectionAngle,
                    layer.SpeedMultiplier,
                    layer.Mode,
                    layer.Origin));
            }
        }

        /// <summary>
        /// Bakes the current multipliers into the authored layers and resets them to 1. The
        /// "Apply" buttons in the inspector call this so a tuned look can become the new baseline.
        /// </summary>
        public void ApplyMultipliers(bool wavelength, bool amplitude, bool steepness)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                WaveLayer layer = _layers[i];
                if (layer == null)
                    continue;

                if (wavelength)
                    layer.Wavelength = layer.Wavelength * _wavelengthMultiplier;
                if (amplitude)
                    layer.Amplitude = layer.Amplitude * _amplitudeMultiplier;
                if (steepness)
                    layer.Steepness = layer.Steepness * _steepnessMultiplier;
            }

            if (wavelength)
                _wavelengthMultiplier = 1f;
            if (amplitude)
                _amplitudeMultiplier = 1f;
            if (steepness)
                _steepnessMultiplier = 1f;
        }

        /// <summary>Enables or disables every layer at once.</summary>
        public void SetAllLayersEnabled(bool enabled)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i] != null)
                    _layers[i].Enabled = enabled;
            }
        }

        /// <summary>
        /// Replaces the layer list with a freshly generated bank from the stored settings.
        /// </summary>
        public void RegenerateLayers()
        {
            WaveProfileGenerator.Generate(_generation, _layers);
        }

        private void OnValidate()
        {
            _wavelengthMultiplier = Mathf.Max(0.001f, _wavelengthMultiplier);
            _amplitudeMultiplier = Mathf.Max(0f, _amplitudeMultiplier);
            _steepnessMultiplier = Mathf.Max(0f, _steepnessMultiplier);

            if (_layers.Count > MaxLayers)
                _layers.RemoveRange(MaxLayers, _layers.Count - MaxLayers);
        }

        private static float EvaluateCurve(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0)
                return 1f;

            return Mathf.Max(0f, curve.Evaluate(t));
        }

        private static AnimationCurve ConstantCurve()
        {
            return AnimationCurve.Constant(0f, 1f, 1f);
        }
    }
}
