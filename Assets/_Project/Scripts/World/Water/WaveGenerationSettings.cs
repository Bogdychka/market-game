using System;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Seeded description of a whole wave bank. Stored inside the profile so the generated layers
    /// stay reproducible: same settings and same seed always rebuild the identical bank.
    /// </summary>
    [Serializable]
    public sealed class WaveGenerationSettings
    {
        [Tooltip("Any change to this number reshuffles the bank; the same number always " +
            "rebuilds the same waves.")]
        [SerializeField] private int _seed = 1337;

        [Tooltip("How many Gerstner layers to generate.")]
        [SerializeField] [Range(1, WaveProfile.MaxLayers)] private int _layerCount = 4;

        [Tooltip("Shortest and longest crest-to-crest distance in metres. Layers descend from " +
            "the long swell to the short chop.")]
        [SerializeField] private Vector2 _minMaxWavelength = new(2f, 16f);

        [Tooltip("Amplitude share over normalized wavelength: 0 = the shortest layer, " +
            "1 = the longest.")]
        [SerializeField] private AnimationCurve _amplitudeByLength =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Smallest and largest layer amplitude in metres.")]
        [SerializeField] private Vector2 _minMaxAmplitude = new(0.02f, 0.35f);

        [Tooltip("Steepness share over normalized wavelength: 0 = the shortest layer, " +
            "1 = the longest.")]
        [SerializeField] private AnimationCurve _steepnessByLength =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Smallest and largest layer steepness.")]
        [SerializeField] private Vector2 _minMaxSteepness = new(0.2f, 0.55f);

        [Tooltip("Direction the generated bank travels, in degrees.")]
        [SerializeField] private float _baseDirectionAngle = 25f;

        [Tooltip("Total spread of the generated directions around the base angle, in degrees. " +
            "0 makes every crest parallel; 180 fans them over a half circle.")]
        [SerializeField] [Range(0f, 360f)] private float _directionAngleVariation = 120f;

        [Tooltip("How much each wavelength may drift off its slot in the descent, 0 = an even " +
            "ladder. Some drift stops the bank from beating in a visible pattern.")]
        [SerializeField] [Range(0f, 0.5f)] private float _wavelengthJitter = 0.15f;

        [Tooltip("Travel mode given to every generated layer.")]
        [SerializeField] private WaveLayerMode _mode = WaveLayerMode.Directional;

        [Tooltip("World XZ point circular layers radiate from.")]
        [SerializeField] private Vector2 _origin;

        /// <summary>Reproducibility seed.</summary>
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        /// <summary>Number of layers to generate.</summary>
        public int LayerCount
        {
            get => _layerCount;
            set => _layerCount = Mathf.Clamp(value, 1, WaveProfile.MaxLayers);
        }

        /// <summary>Shortest (x) and longest (y) wavelength in metres.</summary>
        public Vector2 MinMaxWavelength
        {
            get => _minMaxWavelength;
            set => _minMaxWavelength = value;
        }

        /// <summary>Amplitude share over normalized wavelength.</summary>
        public AnimationCurve AmplitudeByLength
        {
            get => _amplitudeByLength;
            set => _amplitudeByLength = value;
        }

        /// <summary>Smallest (x) and largest (y) amplitude in metres.</summary>
        public Vector2 MinMaxAmplitude
        {
            get => _minMaxAmplitude;
            set => _minMaxAmplitude = value;
        }

        /// <summary>Steepness share over normalized wavelength.</summary>
        public AnimationCurve SteepnessByLength
        {
            get => _steepnessByLength;
            set => _steepnessByLength = value;
        }

        /// <summary>Smallest (x) and largest (y) steepness.</summary>
        public Vector2 MinMaxSteepness
        {
            get => _minMaxSteepness;
            set => _minMaxSteepness = value;
        }

        /// <summary>Direction the generated bank travels, in degrees.</summary>
        public float BaseDirectionAngle
        {
            get => _baseDirectionAngle;
            set => _baseDirectionAngle = value;
        }

        /// <summary>Total angular spread of the generated directions, in degrees.</summary>
        public float DirectionAngleVariation
        {
            get => _directionAngleVariation;
            set => _directionAngleVariation = Mathf.Clamp(value, 0f, 360f);
        }

        /// <summary>Relative drift allowed per wavelength slot.</summary>
        public float WavelengthJitter
        {
            get => _wavelengthJitter;
            set => _wavelengthJitter = Mathf.Clamp(value, 0f, 0.5f);
        }

        /// <summary>Travel mode given to every generated layer.</summary>
        public WaveLayerMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>World XZ origin given to circular layers.</summary>
        public Vector2 Origin
        {
            get => _origin;
            set => _origin = value;
        }

        /// <summary>Shortest generated wavelength, ordered regardless of how the pair is typed.</summary>
        public float MinWavelength =>
            Mathf.Max(0.05f, Mathf.Min(_minMaxWavelength.x, _minMaxWavelength.y));

        /// <summary>Longest generated wavelength.</summary>
        public float MaxWavelength =>
            Mathf.Max(MinWavelength, Mathf.Max(_minMaxWavelength.x, _minMaxWavelength.y));

        /// <summary>Smallest generated amplitude.</summary>
        public float MinAmplitude =>
            Mathf.Max(0f, Mathf.Min(_minMaxAmplitude.x, _minMaxAmplitude.y));

        /// <summary>Largest generated amplitude.</summary>
        public float MaxAmplitude =>
            Mathf.Max(MinAmplitude, Mathf.Max(_minMaxAmplitude.x, _minMaxAmplitude.y));

        /// <summary>Smallest generated steepness.</summary>
        public float MinSteepness =>
            Mathf.Clamp01(Mathf.Min(_minMaxSteepness.x, _minMaxSteepness.y));

        /// <summary>Largest generated steepness.</summary>
        public float MaxSteepness =>
            Mathf.Clamp01(Mathf.Max(_minMaxSteepness.x, _minMaxSteepness.y));

        /// <summary>Copies every setting from another instance.</summary>
        public void CopyFrom(WaveGenerationSettings other)
        {
            if (other == null)
                return;

            _seed = other._seed;
            _layerCount = other._layerCount;
            _minMaxWavelength = other._minMaxWavelength;
            _amplitudeByLength = other._amplitudeByLength;
            _minMaxAmplitude = other._minMaxAmplitude;
            _steepnessByLength = other._steepnessByLength;
            _minMaxSteepness = other._minMaxSteepness;
            _baseDirectionAngle = other._baseDirectionAngle;
            _directionAngleVariation = other._directionAngleVariation;
            _wavelengthJitter = other._wavelengthJitter;
            _mode = other._mode;
            _origin = other._origin;
        }
    }
}
