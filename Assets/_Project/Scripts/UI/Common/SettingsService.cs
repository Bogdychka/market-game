using System;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Runtime settings state. Loads from PlayerPrefs on construction, persists on every change.
    /// Register in ServiceLocator so other systems (FirstPersonController, future AudioMixer) can subscribe.
    /// </summary>
    public class SettingsService
    {
        private const string KeySens    = "s.sens";
        private const string KeyInvertY = "s.invertY";
        private const string KeyMaster  = "s.masterVol";
        private const string KeyMusic   = "s.musicVol";
        private const string KeySfx     = "s.sfxVol";
        private const string KeyRebinds = "s.rebinds";

        public float MouseSensitivity { get; private set; }
        public bool  InvertY          { get; private set; }
        public float MasterVolume     { get; private set; }
        public float MusicVolume      { get; private set; }
        public float SfxVolume        { get; private set; }

        /// <summary>Fired when mouse sensitivity or invert-Y change.</summary>
        public event Action<float, bool> LookSettingsChanged;

        /// <summary>Fired when any volume changes (master, music, sfx).</summary>
        public event Action<float, float, float> VolumesChanged;

        private readonly SettingsSO _defaults;

        public SettingsService(SettingsSO defaults)
        {
            _defaults = defaults;
            Load();
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = value;
            PlayerPrefs.SetFloat(KeySens, value);
            PlayerPrefs.Save();
            LookSettingsChanged?.Invoke(MouseSensitivity, InvertY);
        }

        public void SetInvertY(bool value)
        {
            InvertY = value;
            PlayerPrefs.SetInt(KeyInvertY, value ? 1 : 0);
            PlayerPrefs.Save();
            LookSettingsChanged?.Invoke(MouseSensitivity, InvertY);
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = value;
            PlayerPrefs.SetFloat(KeyMaster, value);
            PlayerPrefs.Save();
            VolumesChanged?.Invoke(MasterVolume, MusicVolume, SfxVolume);
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = value;
            PlayerPrefs.SetFloat(KeyMusic, value);
            PlayerPrefs.Save();
            VolumesChanged?.Invoke(MasterVolume, MusicVolume, SfxVolume);
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = value;
            PlayerPrefs.SetFloat(KeySfx, value);
            PlayerPrefs.Save();
            VolumesChanged?.Invoke(MasterVolume, MusicVolume, SfxVolume);
        }

        /// <summary>Returns the saved input-binding override JSON, or empty string if none.</summary>
        public string GetRebindsJson() => PlayerPrefs.GetString(KeyRebinds, string.Empty);

        /// <summary>Persists input-binding override JSON produced by InputActionAsset.SaveBindingOverridesAsJson.</summary>
        public void SaveRebindsJson(string json)
        {
            PlayerPrefs.SetString(KeyRebinds, json);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(KeySens,    _defaults.defaultMouseSensitivity);
            InvertY          = PlayerPrefs.GetInt(KeyInvertY,   _defaults.defaultInvertY ? 1 : 0) == 1;
            MasterVolume     = PlayerPrefs.GetFloat(KeyMaster,  _defaults.defaultMasterVolume);
            MusicVolume      = PlayerPrefs.GetFloat(KeyMusic,   _defaults.defaultMusicVolume);
            SfxVolume        = PlayerPrefs.GetFloat(KeySfx,     _defaults.defaultSfxVolume);
        }
    }
}
