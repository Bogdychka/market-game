using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for getting package information from Unity package manifest files.
    /// </summary>
    public class GetPackagesResource : McpResourceBase
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string LockPath = "Packages/packages-lock.json";

        public GetPackagesResource()
        {
            Name = "get_packages";
            Description = "Retrieve resolved packages from manifest.json and packages-lock.json without blocking Package Manager";
            Uri = "unity://packages";
        }
        
        /// <summary>
        /// Execute the resource to get package information from project files.
        /// </summary>
        /// <param name="parameters">Optional parameters for filtering</param>
        /// <returns>JObject containing package information</returns>
        public override JObject Fetch(JObject parameters)
        {
            try
            {
                JObject manifest = LoadJsonFile(ManifestPath);
                JObject packageLock = LoadJsonFile(LockPath);

                JObject manifestDependencies = manifest?["dependencies"] as JObject ?? new JObject();
                JObject lockDependencies = packageLock?["dependencies"] as JObject ?? new JObject();

                JArray manifestPackages = BuildManifestPackages(manifestDependencies, lockDependencies);
                JArray resolvedPackages = BuildResolvedPackages(lockDependencies, manifestDependencies);

                return new JObject
                {
                    ["success"] = true,
                    ["message"] = $"Retrieved {manifestPackages.Count} manifest packages and {resolvedPackages.Count} resolved packages from package files",
                    ["projectPackages"] = resolvedPackages,
                    ["registryPackages"] = new JArray(),
                    ["manifestPackages"] = manifestPackages,
                    ["resolvedPackages"] = resolvedPackages,
                    ["source"] = "manifest_and_packages_lock",
                    ["manifestPath"] = ManifestPath,
                    ["packagesLockPath"] = LockPath
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"Failed to read package files: {ex.Message}",
                    ["projectPackages"] = new JArray(),
                    ["registryPackages"] = new JArray(),
                    ["manifestPackages"] = new JArray(),
                    ["resolvedPackages"] = new JArray()
                };
            }
        }

        private JArray BuildManifestPackages(JObject manifestDependencies, JObject lockDependencies)
        {
            JArray result = new JArray();

            foreach (JProperty dependency in manifestDependencies.Properties())
            {
                JObject lockInfo = lockDependencies[dependency.Name] as JObject;
                result.Add(PackageToJObject(
                    dependency.Name,
                    dependency.Value?.ToObject<string>() ?? string.Empty,
                    lockInfo,
                    declaredDirect: true));
            }

            return result;
        }

        private JArray BuildResolvedPackages(JObject lockDependencies, JObject manifestDependencies)
        {
            JArray result = new JArray();

            foreach (JProperty dependency in lockDependencies.Properties())
            {
                JObject lockInfo = dependency.Value as JObject;
                bool declaredDirect = manifestDependencies.ContainsKey(dependency.Name);
                string declaredVersion = declaredDirect
                    ? manifestDependencies[dependency.Name]?.ToObject<string>() ?? string.Empty
                    : string.Empty;

                result.Add(PackageToJObject(dependency.Name, declaredVersion, lockInfo, declaredDirect));
            }

            return result;
        }

        private JObject PackageToJObject(string packageName, string declaredVersion, JObject lockInfo, bool declaredDirect)
        {
            string resolvedVersion = lockInfo?["version"]?.ToObject<string>() ?? declaredVersion;
            string source = lockInfo?["source"]?.ToObject<string>() ?? InferSource(declaredVersion);
            int depth = lockInfo?["depth"]?.ToObject<int?>() ?? (declaredDirect ? 0 : -1);

            return new JObject
            {
                ["name"] = packageName,
                ["displayName"] = packageName,
                ["version"] = resolvedVersion,
                ["declaredVersion"] = declaredVersion,
                ["description"] = string.Empty,
                ["category"] = string.Empty,
                ["source"] = source,
                ["state"] = declaredDirect ? "installed" : "dependency",
                ["depth"] = depth,
                ["isDirectDependency"] = declaredDirect,
                ["url"] = lockInfo?["url"]?.ToObject<string>() ?? string.Empty,
                ["dependencies"] = lockInfo?["dependencies"] as JObject ?? new JObject(),
                ["author"] = new JObject
                {
                    ["name"] = string.Empty,
                    ["email"] = string.Empty,
                    ["url"] = string.Empty
                }
            };
        }

        private JObject LoadJsonFile(string projectRelativePath)
        {
            string fullPath = GetProjectRelativeFullPath(projectRelativePath);
            if (!File.Exists(fullPath))
            {
                return new JObject();
            }

            string json = File.ReadAllText(fullPath);
            return JObject.Parse(json);
        }

        private string GetProjectRelativeFullPath(string projectRelativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private string InferSource(string declaredVersion)
        {
            if (string.IsNullOrEmpty(declaredVersion))
            {
                return string.Empty;
            }

            if (declaredVersion.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return "embedded";
            }

            if (declaredVersion.StartsWith("git:", StringComparison.OrdinalIgnoreCase) ||
                declaredVersion.Contains("github.com"))
            {
                return "git";
            }

            return "registry";
        }
    }
}
