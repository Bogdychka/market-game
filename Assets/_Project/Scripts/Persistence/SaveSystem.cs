using System;
using System.IO;
using UnityEngine;

namespace Market.Persistence
{
    /// <summary>
    /// Хранит сохранение в JSON в Application.persistentDataPath.
    /// Регистрируется в ServiceLocator из GameBootstrap. Чистый C# класс — без MonoBehaviour.
    /// </summary>
    public class SaveSystem
    {
        private const string SaveFileName = "save.json";
        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// Флаг, выставляемый MainMenuController при «Продолжить».
        /// GameSaver проверяет его в Start() и при необходимости загружает.
        /// </summary>
        public bool ShouldLoadOnStart { get; set; }

        public bool HasSave() => File.Exists(SavePath);

        public bool Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[SaveSystem] Сохранено: {SavePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Ошибка сохранения: {e.Message}");
                return false;
            }
        }

        public SaveData Load()
        {
            if (!HasSave())
            {
                Debug.LogWarning("[SaveSystem] Файл сохранения не найден.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                Debug.Log("[SaveSystem] Загружено.");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Ошибка загрузки: {e.Message}");
                return null;
            }
        }

        public void DeleteSave()
        {
            if (!HasSave()) return;
            try
            {
                File.Delete(SavePath);
                Debug.Log("[SaveSystem] Сохранение удалено.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Ошибка удаления: {e.Message}");
            }
        }
    }
}
