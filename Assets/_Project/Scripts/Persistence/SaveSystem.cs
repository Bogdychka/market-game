using System;
using System.IO;
using UnityEngine;

namespace Market.Persistence
{
    /// <summary>
    /// Stores the save file as JSON under Application.persistentDataPath.
    /// Registered in ServiceLocator from GameBootstrap. Plain C# class -- no MonoBehaviour.
    /// </summary>
    public class SaveSystem
    {
        private const string SaveFileName = "save.json";
        private string SavePath   => Path.Combine(Application.persistentDataPath, SaveFileName);
        private string TempPath   => SavePath + ".tmp";
        private string BackupPath => SavePath + ".bak";

        /// <summary>
        /// Flag set by MainMenuController when the player clicks Continue.
        /// GameSaver checks it in Start() and loads if true.
        /// </summary>
        public bool ShouldLoadOnStart { get; set; }

        public bool HasSave() => File.Exists(SavePath) || File.Exists(BackupPath);

        /// <summary>
        /// Writes the save atomically: serialize to a temp file, then swap it into place keeping the
        /// previous save as a .bak. A crash mid-write can never truncate the live save file.
        /// </summary>
        public bool Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(TempPath, json);

                if (File.Exists(SavePath))
                    // Atomic swap; keeps the prior good save as .bak.
                    File.Replace(TempPath, SavePath, BackupPath);
                else
                    File.Move(TempPath, SavePath);

                Debug.Log($"[SaveSystem] Saved: {SavePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save error: {e.Message}");
                TryCleanupTemp();
                return false;
            }
        }

        public SaveData Load()
        {
            if (TryLoadFrom(SavePath, out SaveData data))
                return data;

            // Primary missing or corrupt -> fall back to the last good backup.
            if (File.Exists(BackupPath) && TryLoadFrom(BackupPath, out data))
            {
                Debug.LogWarning("[SaveSystem] Loaded from backup after primary save was unreadable.");
                return data;
            }

            Debug.LogWarning("[SaveSystem] No readable save file found.");
            return null;
        }

        private bool TryLoadFrom(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return false;

                Debug.Log($"[SaveSystem] Loaded: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load error ({path}): {e.Message}");
                return false;
            }
        }

        public void DeleteSave()
        {
            TryDelete(SavePath);
            TryDelete(BackupPath);
            TryCleanupTemp();
        }

        private void TryDelete(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
                Debug.Log($"[SaveSystem] Deleted: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Delete error ({path}): {e.Message}");
            }
        }

        private void TryCleanupTemp()
        {
            try
            {
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Temp cleanup failed: {e.Message}");
            }
        }
    }
}
