using System.IO;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot converter for the Cartoon_Farm_Crops pack: swaps built-in Standard materials to
    /// URP/Lit and copies the base texture/color, so crops don't render magenta under URP (audit M1).
    /// Re-runnable and idempotent (skips materials already on a URP shader).
    /// </summary>
    public static class CropMaterialUrpUpgrader
    {
        private const string CropMaterialsFolder = "Assets/Cartoon_Farm_Crops/Materials";
        private const string NatureMaterialsFolder = "Assets/SimpleNaturePack/Materials";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("Market/Debug/Convert Crop Materials to URP")]
        public static void ConvertCropMaterials()
        {
            ConvertMaterialsInFolder(CropMaterialsFolder, "CropMaterialUrpUpgrader");
        }

        /// <summary>
        /// Converts the imported Simple Nature Pack materials to URP/Lit.
        /// </summary>
        [MenuItem("Market/Debug/Convert Simple Nature Materials to URP")]
        public static void ConvertSimpleNatureMaterials()
        {
            ConvertMaterialsInFolder(NatureMaterialsFolder, "NatureMaterialUrpUpgrader");
        }

        private static void ConvertMaterialsInFolder(string materialsFolder, string logSource)
        {
            Shader urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                Debug.LogError($"[{logSource}] Shader not found: {UrpLitShaderName}. Is URP installed?");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { materialsFolder });
            int converted = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == urpLit)
                    continue;

                // Capture base look from the old Standard shader before swapping.
                Texture baseMap = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Color baseColor = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                material.shader = urpLit;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseMap);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);

                EditorUtility.SetDirty(material);
                converted++;
                Debug.Log($"[{logSource}] Converted to URP/Lit: {Path.GetFileName(path)}");
            }

            if (converted > 0)
                AssetDatabase.SaveAssets();

            Debug.Log($"[{logSource}] Done. Converted {converted} material(s).");
        }
    }
}
