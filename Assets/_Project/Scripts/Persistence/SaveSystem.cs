using System;
using System.IO;
using UnityEngine;

namespace Market.Persistence
{
    /// <summary>
    /// Stores the save file as JSON under Application.persistentDataPath.
    /// Registered in ServiceLocator from GameBootstrap. Plain C# class — no MonoBehaviour.
    /// </summary>
    public class SaveSystem
    {
        private const string SaveFileName = "save.json";
        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// Flag set by MainMenuController when the player clicks Continue.
        /// GameSaver checks it in Start() and loads if true.
        /// </summary>
        public bool ShouldLoadOnStart { get; set; }

        public bool HasSave() => File.Exists(SavePath);

        public bool Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[SaveSystem] Saved: {SavePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save error: {e.Message}");
                return false;
            }
        }

        public SaveData Load()
        {
            if (!HasSave())
            {
                Debug.LogWarning("[SaveSystem] Save file not found.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                Debug.Log("[SaveSystem] Loaded.");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load error: {e.Message}");
                return null;
            }
        }

        public void DeleteSave()
        {
            if (!HasSave()) return;
            try
            {
                File.Delete(SavePath);
                Debug.Log("[SaveSystem] Save deleted.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Delete error: {e.Message}");
            }
        }
    }
}
