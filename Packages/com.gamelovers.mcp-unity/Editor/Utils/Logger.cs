using UnityEngine;
using UnityEditor;
using McpUnity.Unity;

namespace McpUnity.Utils
{
    /// <summary>
    /// Special logger to use inside the MCP Unity Editor project
    /// </summary>
    public static class McpLogger
    {
        private const string LogPrefix = "[MCP Unity] ";
        
        /// <summary>
        /// Log an info message if info logs are enabled
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogInfo(string message)
        {
            if (McpUnitySettings.Instance.EnableInfoLogs)
            {
                Debug.Log($"{LogPrefix}{message}");
            }
        }
        
        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{LogPrefix}{message}");
        }
        
        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogError(string message)
        {
            Debug.LogError($"{LogPrefix}{message}");
        }
    }

    /// <summary>
    /// Compatibility helpers for Unity 6 EntityId APIs while preserving the package's existing instanceId JSON field.
    /// </summary>
    public static class ObjectIdUtils
    {
        /// <summary>
        /// Resolve the legacy integer instance ID used by the MCP schema to a Unity object.
        /// </summary>
        public static Object ObjectFromLegacyInstanceId(int instanceId)
        {
#pragma warning disable CS0618
            return EditorUtility.EntityIdToObject(instanceId);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Return the legacy integer instance ID used by the MCP schema.
        /// </summary>
        public static int GetLegacyInstanceId(Object unityObject)
        {
            if (unityObject == null)
            {
                return 0;
            }

#pragma warning disable CS0618
            return unityObject.GetEntityId();
#pragma warning restore CS0618
        }

        /// <summary>
        /// Return the raw Unity 6 EntityId value for future non-32-bit-safe consumers.
        /// </summary>
        public static ulong GetEntityIdValue(Object unityObject)
        {
            if (unityObject == null)
            {
                return 0UL;
            }

            return EntityId.ToULong(unityObject.GetEntityId());
        }
    }
}
