using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>Caches asset searches and tracks unique assets during one scan.</summary>
    public sealed class ProjectHealthContext
    {
        public const string ProjectRoot = "Assets/_Project";

        private readonly Dictionary<string, string[]> _pathCache = new();
        private readonly HashSet<string> _checkedPaths = new(StringComparer.Ordinal);

        public string[] FindAssetPaths(string filter)
        {
            if (_pathCache.TryGetValue(filter, out string[] cached))
                return cached;

            string[] guids = AssetDatabase.FindAssets(filter, new[] { ProjectRoot });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            _pathCache.Add(filter, paths);
            return paths;
        }

        public T Load<T>(string path) where T : UnityEngine.Object
        {
            Track(path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public void Track(string path)
        {
            if (!string.IsNullOrEmpty(path))
                _checkedPaths.Add(path);
        }

        public int CheckedAssetCount => _checkedPaths.Count;
    }
}
