using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.Core
{
    /// <summary>
    /// Загрузка сцен. Регистрируется в ServiceLocator из Bootstrap.
    /// Сама не MonoBehaviour, использует Coroutine через переданный runner.
    /// </summary>
    public class SceneLoader
    {
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;

        private readonly MonoBehaviour _coroutineRunner;
        private bool _isLoading;

        /// <summary>true пока идёт асинхронная загрузка сцены.</summary>
        public bool IsLoading => _isLoading;

        public SceneLoader(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        public void Load(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoader] Имя сцены пустое — загрузка отменена.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Сцена '{sceneName}' не найдена в Build Settings или недоступна.");
                return;
            }

            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] Загрузка уже идёт — запрос '{sceneName}' проигнорирован.");
                return;
            }

            Debug.Log($"[SceneLoader] Запрос загрузки: {SceneManager.GetActiveScene().name} -> {sceneName}");
            _coroutineRunner.StartCoroutine(LoadRoutine(sceneName));
        }

        public IEnumerator LoadRoutine(string sceneName)
        {
            _isLoading = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!op.isDone)
                yield return null;

            _isLoading = false;
            OnSceneLoadCompleted?.Invoke(sceneName);
            Debug.Log($"[SceneLoader] Загружена сцена: {sceneName}");
        }
    }
}
