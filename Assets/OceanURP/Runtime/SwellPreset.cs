using UnityEngine;

namespace OceanSystem
{
    [CreateAssetMenu(fileName = "New Swell", menuName = "Ocean/Swell")]
    public class SwellPreset : ScriptableObject
    {
        [SerializeField] private SpectrumParams _spectrum = SpectrumParams.GetDefaultSwell();

        [SerializeField] private float _referenceWaveHeight;

        public float ReferenceWaveHeight => _referenceWaveHeight;
        public SpectrumParams Spectrum => _spectrum;
    }
}
