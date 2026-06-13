using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Default values for all player-configurable settings.
    /// Runtime state lives in <see cref="SettingsService"/>; this SO is read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "SettingsSO", menuName = "Market/Settings")]
    public class SettingsSO : ScriptableObject
    {
        [Header("Look")]
        public float defaultMouseSensitivity = 0.12f;
        public float mouseSensitivityMin    = 0.02f;
        public float mouseSensitivityMax    = 0.60f;
        public bool  defaultInvertY         = false;

        [Header("Volumes (0-1)")]
        public float defaultMasterVolume = 1f;
        public float defaultMusicVolume  = 0.8f;
        public float defaultSfxVolume    = 1f;
    }
}
