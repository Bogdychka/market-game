using System;
using System.Collections.Generic;
using UnityEngine;

namespace Market.Core
{
    /// <summary>
    /// Lightweight service registry. Registered from Bootstrap, accessible anywhere.
    /// Do not treat as a god-object -- use only for game-wide systems
    /// (Money, Inventory, Time, EventBus, SceneLoader, etc.).
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// Play mode runs without a domain reload, so statics survive between sessions. Without
        /// this, a second Play would find services pointing at destroyed objects from the first.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _services.Clear();
        }

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting service {type.Name}");
            }
            _services[type] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;

            Debug.LogError($"[ServiceLocator] Service {typeof(T).Name} is not registered");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        public static void Clear()
        {
            _services.Clear();
        }
    }
}
