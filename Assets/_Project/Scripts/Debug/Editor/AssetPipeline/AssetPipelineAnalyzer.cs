using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>Performs a read-only analysis of one FBX or OBJ model asset.</summary>
    public sealed class AssetPipelineAnalyzer
    {
        public AssetPipelineReport Analyze(GameObject model, AssetPipelineProfileId profileId)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                throw new ArgumentException("Select an imported FBX or OBJ model asset.", nameof(model));

            var report = new AssetPipelineReport
            {
                AssetPath = path,
                Profile = profileId,
                ImportScale = importer.globalScale
            };
            MarketAssetProfile profile = MarketAssetProfile.Get(profileId);

            AnalyzeGeometry(model, profile, report);
            AnalyzeTransforms(model, profile, report);
            AnalyzeMaterials(model, report);
            AnalyzeImporter(importer, profile, report);
            AnalyzeWorkflow(model, path, report);
            return report;
        }

        public static bool IsModelAsset(GameObject model)
        {
            if (model == null)
                return false;

            string path = AssetDatabase.GetAssetPath(model);
            return AssetImporter.GetAtPath(path) is ModelImporter;
        }

        private static void AnalyzeGeometry(
            GameObject model,
            MarketAssetProfile profile,
            AssetPipelineReport report)
        {
            var meshes = new HashSet<Mesh>();
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                AddMesh(filter.sharedMesh, filter.transform.localToWorldMatrix, meshes, ref bounds, ref hasBounds);
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AddMesh(renderer.sharedMesh, renderer.transform.localToWorldMatrix, meshes, ref bounds, ref hasBounds);

            CollectMeshCounts(meshes, report);
            if (!hasBounds)
            {
                report.Add(AssetPipelineSeverity.Error, "No mesh geometry", "The selected model contains no readable mesh bounds.");
                return;
            }

            report.Dimensions = bounds.size;
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (AssetPipelineRules.IsSuspiciousSize(largest, profile))
                report.Add(AssetPipelineSeverity.Warning, "Size is outside the profile range",
                    $"Largest dimension is {largest:0.###} m; {profile.Id} expects {profile.MinimumSize:0.###}-{profile.MaximumSize:0.###} m.");
            if (report.TriangleCount > profile.TriangleLimit)
                report.Add(AssetPipelineSeverity.Warning, "Triangle budget exceeded",
                    $"Model has {report.TriangleCount} triangles; the broad {profile.Id} limit is {profile.TriangleLimit}.");

            AnalyzePivot(model.transform.position.y, bounds, profile, report);
        }

        private static void AddMesh(
            Mesh mesh,
            Matrix4x4 matrix,
            ISet<Mesh> meshes,
            ref Bounds combined,
            ref bool initialized)
        {
            if (mesh == null)
                return;

            meshes.Add(mesh);
            Bounds transformed = TransformBounds(mesh.bounds, matrix);
            if (!initialized)
            {
                combined = transformed;
                initialized = true;
            }
            else
            {
                combined.Encapsulate(transformed);
            }
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            Vector3 worldExtents = new(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, worldExtents * 2f);
        }

        private static void CollectMeshCounts(IEnumerable<Mesh> meshes, AssetPipelineReport report)
        {
            foreach (Mesh mesh in meshes)
            {
                report.MeshCount++;
                report.VertexCount += mesh.vertexCount;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    report.TriangleCount += (long)mesh.GetIndexCount(subMesh) / 3L;
            }
        }

        private static void AnalyzePivot(
            float pivotY,
            Bounds bounds,
            MarketAssetProfile profile,
            AssetPipelineReport report)
        {
            if (!profile.IsStatic || bounds.size.y <= 0.0001f)
                return;

            float bottomOffsetRatio = Mathf.Abs(pivotY - bounds.min.y) / bounds.size.y;
            if (bottomOffsetRatio > 0.2f)
                report.Add(AssetPipelineSeverity.Warning, "Pivot is not near the model base",
                    $"Pivot is {bottomOffsetRatio:P0} of model height above or below the bottom. Fix the pivot in Blender when ground placement matters.");
        }

        private static void AnalyzeTransforms(
            GameObject model,
            MarketAssetProfile profile,
            AssetPipelineReport report)
        {
            int genericNames = 0;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (AssetPipelineRules.HasInvalidScale(child.localScale))
                    report.Add(AssetPipelineSeverity.Error, "Invalid transform scale",
                        $"'{HierarchyPath(child)}' has zero or negative local scale {child.localScale}.");

                if (genericNames < 5 && AssetPipelineRules.IsGenericObjectName(child.name))
                {
                    report.Add(AssetPipelineSeverity.Warning, "Generic Blender object name",
                        $"Rename '{HierarchyPath(child)}' in the source file for maintainable prefab hierarchies.");
                    genericNames++;
                }
            }

            if (!profile.IsStatic && model.GetComponentInChildren<Animator>(true) == null)
                report.Add(AssetPipelineSeverity.Warning, "Character has no Animator",
                    "The Character profile expects an imported rig or an Animator in the model hierarchy.");
        }

        private static void AnalyzeMaterials(GameObject model, AssetPipelineReport report)
        {
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        report.Add(AssetPipelineSeverity.Error, "Missing material reference",
                            $"Renderer '{renderer.name}' contains an empty material slot.");
                        continue;
                    }

                    if (materials.Add(material) &&
                        (material.shader == null ||
                         !material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)))
                    {
                        report.Add(AssetPipelineSeverity.Warning, "Material is not using a URP shader",
                            $"'{material.name}' uses '{material.shader?.name ?? "Missing Shader"}'. Convert deliberately; materials are never changed automatically.");
                    }
                }
            }

            report.MaterialCount = materials.Count;
            if (materials.Count == 0)
                report.Add(AssetPipelineSeverity.Warning, "No materials assigned", "The model has no non-null material references.");
        }

        private static void AnalyzeImporter(
            ModelImporter importer,
            MarketAssetProfile profile,
            AssetPipelineReport report)
        {
            if (importer.importCameras)
                report.Add(AssetPipelineSeverity.Warning, "Camera import is enabled", "Static art normally should not import Blender cameras.");
            if (importer.importLights)
                report.Add(AssetPipelineSeverity.Warning, "Light import is enabled", "Static art normally should not import Blender lights.");
            if (profile.IsStatic && (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None))
                report.Add(AssetPipelineSeverity.Warning, "Static model imports animation", "Use the confirmed static preset if this model is not animated.");
            if (!profile.IsStatic && !importer.importAnimation)
                report.Add(AssetPipelineSeverity.Error, "Character animation import is disabled", "Enable animation import for a rigged Character asset.");
            if (profile.IsStatic && importer.isReadable)
                report.Add(AssetPipelineSeverity.Info, "Read/Write is enabled", "Disable it when runtime mesh access is not required.");
        }

        private static void AnalyzeWorkflow(GameObject model, string path, AssetPipelineReport report)
        {
            report.HasCollider = model.GetComponentInChildren<Collider>(true) != null;
            if (!report.HasCollider)
                report.Add(AssetPipelineSeverity.Info, "No collider in model", "Create a wrapper prefab with a bounds-based BoxCollider when gameplay collision is required.");

            report.HasProjectPrefab = HasProjectPrefab(path);
            if (!report.HasProjectPrefab)
                report.Add(AssetPipelineSeverity.Info, "No project wrapper prefab", "Create a project-owned wrapper before adding gameplay components.");
        }

        private static bool HasProjectPrefab(string modelPath)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/_Project/Art/Prefabs" });
            foreach (string guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.GetDependencies(prefabPath, false).Contains(modelPath))
                    return true;
            }

            return false;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
