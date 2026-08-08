using UnityEngine;

namespace OceanSystem
{
    [CreateAssetMenu(fileName = "New Local Waves", menuName = "Ocean/Local Waves")]
    public class LocalWavesPreset : ScriptableObject
    {
        [Header("Waves")]
        [SerializeField] private SpectrumParams _spectrum = SpectrumParams.GetDefaultLocal();
        [SerializeField] private float _referenceWaveHeight = 1;
        [SerializeField, Range(0, 2)] private float _chop = 1;

        [Header("Foam")]
        [SerializeField] private FoamParams _foam = FoamParams.GetDefault();

        [Header("Equalizer")]
        [SerializeField] private EqualizerPreset _equalizer;

        [SerializeField, HideInInspector] private float _windForce;

        public float WindForce => _windForce;
        public float ReferenceWaveHeight => _referenceWaveHeight;
        public SpectrumParams Spectrum => _spectrum;
        public FoamParams Foam => _foam;
        public float Chop => _chop;
        public EqualizerPreset Equalizer => _equalizer;
    }
}
