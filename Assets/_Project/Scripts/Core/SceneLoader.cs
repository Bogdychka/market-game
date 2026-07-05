using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Market.Core
{
    /// <summary>
    /// Scene loading service. Registered in ServiceLocator from Bootstrap.
    /// Not a MonoBehaviour -- uses a coroutine runner provided at construction.
    /// </summary>
    public class SceneLoader
    {
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;

        private readonly MonoBehaviour _coroutineRunner;
        private bool _isLoading;

        /// <summary>True while an async scene load is in progress.</summary>
        public bool IsLoading => _isLoading;

        public SceneLoader(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        public void Load(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoader] Scene name is empty -- load cancelled.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' not found in Build Settings or not accessible.");
                return;
            }

            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] Load already in progress -- request '{sceneName}' ignored.");
                return;
            }

            Debug.Log($"[SceneLoader] Load request: {SceneManager.GetActiveScene().name} -> {sceneName}");
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
            Debug.Log($"[SceneLoader] Scene loaded: {sceneName}");
        }
    }
}
