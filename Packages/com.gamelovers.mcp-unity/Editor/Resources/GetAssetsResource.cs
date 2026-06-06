using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for getting asset information from the Unity Asset Database
    /// </summary>
    public class GetAssetsResource : McpResourceBase
    {
        public GetAssetsResource()
        {
            Name = "get_assets";
            Description = "Retrieves assets from the Unity Asset Database";
            Uri = "unity://assets";
        }
        
        /// <summary>
        /// Execute the resource to get asset information
        /// </summary>
        /// <param name="parameters">Optional parameters for filtering</param>
        /// <returns>JObject containing asset information</returns>
        public override JObject Fetch(JObject parameters)
        {
            // Extract optional filter parameters
            string assetType = parameters?["assetType"]?.ToObject<string>();
            string searchPattern = parameters?["searchPattern"]?.ToObject<string>();
            string[] searchFolders = GetSearchFolders(parameters);
                
            // Get all assets from the project
            JArray assets = GetAllAssets(assetType, searchPattern, searchFolders);
                
            // Return result
            return new JObject
            {
                ["success"] = true,
                ["message"] = searchFolders.Length > 0
                    ? $"Retrieved {assets.Count} assets from {string.Join(", ", searchFolders)}"
                    : $"Retrieved {assets.Count} assets",
                ["assets"] = assets
            };
        }
        
        /// <summary>
        /// Get all assets from the project, optionally filtered by type and search pattern
        /// </summary>
        /// <param name="assetType">Optional filter by asset type</param>
        /// <param name="searchPattern">Optional search pattern for asset names</param>
        /// <param name="searchFolders">Optional folder scope for AssetDatabase.FindAssets.</param>
        /// <returns>JArray containing asset information</returns>
        private JArray GetAllAssets(string assetType, string searchPattern, string[] searchFolders)
        {
            JArray result = new JArray();
            
            // Find all assets
            string filter = string.IsNullOrEmpty(searchPattern) ? "" : searchPattern;
            string[] assetGuids = searchFolders.Length > 0
                ? AssetDatabase.FindAssets(filter, searchFolders)
                : AssetDatabase.FindAssets(filter);
            
            foreach (string guid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                // Skip folders
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }
                
                // Get asset type
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    continue;
                }
                
                string fileType = asset.GetType().Name;
                
                // Filter by asset type if specified
                if (!string.IsNullOrEmpty(assetType) && !fileType.Equals(assetType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Create asset information
                JObject assetInfo = new JObject
                {
                    ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                    ["filename"] = Path.GetFileName(assetPath),
                    ["path"] = assetPath,
                    ["type"] = fileType,
                    ["extension"] = Path.GetExtension(assetPath).TrimStart('.'),
                    ["guid"] = guid,
                    ["size"] = GetAssetSize(assetPath)
                };
                
                result.Add(assetInfo);
            }
            
            return result;
        }

        /// <summary>
        /// Parse optional folder filters from MCP parameters.
        /// </summary>
        private string[] GetSearchFolders(JObject parameters)
        {
            var folders = new List<string>();
            var seen = new HashSet<string>();

            AddSearchFolder(parameters?["folder"]?.ToObject<string>(), folders, seen);
            AddSearchFolder(parameters?["folderPath"]?.ToObject<string>(), folders, seen);
            AddSearchFolder(parameters?["assetFolder"]?.ToObject<string>(), folders, seen);
            AddSearchFolders(parameters?["folders"], folders, seen);
            AddSearchFolders(parameters?["folderPaths"], folders, seen);

            return folders.ToArray();
        }

        private void AddSearchFolders(JToken token, List<string> folders, HashSet<string> seen)
        {
            if (token == null)
            {
                return;
            }

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken entry in token)
                {
                    AddSearchFolder(entry?.ToObject<string>(), folders, seen);
                }

                return;
            }

            AddSearchFolder(token.ToObject<string>(), folders, seen);
        }

        private void AddSearchFolder(string folder, List<string> folders, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            if (seen.Add(normalized))
            {
                folders.Add(normalized);
            }
        }
        
        /// <summary>
        /// Get the size of an asset file
        /// </summary>
        /// <param name="assetPath">Path to the asset</param>
        /// <returns>Size in bytes, or -1 if the file cannot be found</returns>
        private long GetAssetSize(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            FileInfo fileInfo = new FileInfo(fullPath);
            return fileInfo.Exists ? fileInfo.Length : -1;
        }
    }
}
