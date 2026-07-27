using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Creates/updates the experimental realistic water material at shader defaults.
    /// </summary>
    public static class RealisticWaterMaterialInstaller
    {
        private const string MaterialPath = "Assets/_Project/Art/Materials/Water/M_RealisticWaterLab.mat";
        private const string ProjectedCausticMaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterProjectedCaustics.mat";
        private const string UnderwaterSurfaceMaterialPath =
            "Assets/_Project/Art/Materials/Water/M_RealisticWaterUnderwaterSurface.mat";
        private const string NormalMapAPath =
            "Assets/_Project/Art/Textures/Water/T_RealisticWater_NormalA.png";
        private const string NormalMapBPath =
            "Assets/_Project/Art/Textures/Water/T_RealisticWater_NormalB.png";
        private const string ShaderName = "Market/World/RealisticWater";
        private const string ProjectedCausticShaderName =
            "Market/World/RealisticWaterProjectedCaustics";
        private const string UnderwaterSurfaceShaderName =
            "Market/World/RealisticWaterUnderwaterSurface";

        [MenuItem("Market/Debug/Water/Create Realistic Water Material")]
        public static void CreateMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[RealisticWaterMaterialInstaller] Shader '{ShaderName}' not found.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_RealisticWaterLab" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            AssignTextureIfAvailable(material, "_NormalMapA", NormalMapAPath);
            AssignTextureIfAvailable(material, "_NormalMapB", NormalMapBPath);
            SetFloatIfAvailable(material, "_RefractionEdgeFade", 0.08f);
            SetFloatIfAvailable(material, "_RefractionDepthScale", 2f);
            SetFloatIfAvailable(material, "_PlanarReflectionStrength", 0.85f);
            SetFloatIfAvailable(material, "_ReflectionEdgeFade", 0.08f);
            SetFloatIfAvailable(material, "_FoamCrestStrength", 1f);
            SetFloatIfAvailable(material, "_FoamShoreStrength", 1f);
            SetFloatIfAvailable(material, "_FoamCrestBias", 0.12f);
            RemoveUndeclaredSavedProperties(material);
            EnsureProjectedCausticMaterial();
            EnsureUnderwaterSurfaceMaterial();
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RealisticWaterMaterialInstaller] Ensured {MaterialPath} using '{ShaderName}'.");
        }

        /// <summary>
        /// Creates or updates the material used by the bounded R7 receiver overlays.
        /// </summary>
        public static Material EnsureProjectedCausticMaterial()
        {
            Shader shader = Shader.Find(ProjectedCausticShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[RealisticWaterMaterialInstaller] Shader " +
                    $"'{ProjectedCausticShaderName}' not found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                ProjectedCausticMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "M_RealisticWaterProjectedCaustics",
                };
                AssetDatabase.CreateAsset(material, ProjectedCausticMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetFloatIfAvailable(material, "_CausticIntensity", 1.1f);
            SetFloatIfAvailable(material, "_CausticTilingA", 0.72f);
            SetFloatIfAvailable(material, "_CausticTilingB", 1.03f);
            SetFloatIfAvailable(material, "_CausticSpeedA", 0.12f);
            SetFloatIfAvailable(material, "_CausticSpeedB", 0.08f);
            SetFloatIfAvailable(material, "_CausticDepthStart", 0.15f);
            SetFloatIfAvailable(material, "_CausticDepthEnd", 12f);
            SetFloatIfAvailable(material, "_CausticTurbidity", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Creates or updates the material used by the optional R8 underside renderer.
        /// </summary>
        public static Material EnsureUnderwaterSurfaceMaterial()
        {
            Shader shader = Shader.Find(UnderwaterSurfaceShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[RealisticWaterMaterialInstaller] Shader " +
                    $"'{UnderwaterSurfaceShaderName}' not found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                UnderwaterSurfaceMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "M_RealisticWaterUnderwaterSurface",
                };
                AssetDatabase.CreateAsset(material, UnderwaterSurfaceMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            AssignTextureIfAvailable(material, "_NormalMapA", NormalMapAPath);
            AssignTextureIfAvailable(material, "_NormalMapB", NormalMapBPath);
            SetFloatIfAvailable(material, "_InternalReflectionStrength", 1f);
            SetFloatIfAvailable(material, "_WaterIOR", 1.333f);
            SetColorIfAvailable(
                material,
                "_UnderwaterFogColor",
                new Color(0.015f, 0.18f, 0.32f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignTextureIfAvailable(
            Material material, string propertyName, string assetPath)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetFloatIfAvailable(
            Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static void SetColorIfAvailable(
            Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, value);
        }

        private static void RemoveUndeclaredSavedProperties(Material material)
        {
            var serializedMaterial = new SerializedObject(material);
            RemoveUndeclaredEntries(
                serializedMaterial.FindProperty("m_SavedProperties.m_TexEnvs"),
                material);
            RemoveUndeclaredEntries(
                serializedMaterial.FindProperty("m_SavedProperties.m_Ints"),
                material);
            RemoveUndeclaredEntries(
                serializedMaterial.FindProperty("m_SavedProperties.m_Floats"),
                material);
            RemoveUndeclaredEntries(
                serializedMaterial.FindProperty("m_SavedProperties.m_Colors"),
                material);
            serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveUndeclaredEntries(
            SerializedProperty entries, Material material)
        {
            if (entries == null || !entries.isArray)
                return;

            for (int index = entries.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty name = entry.FindPropertyRelative("first");
                if (name != null && !material.HasProperty(name.stringValue))
                    entries.DeleteArrayElementAtIndex(index);
            }
        }
    }
}
