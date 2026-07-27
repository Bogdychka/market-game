using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot import fixer for static-prop FBX packs (audit H4): turns off rig and animation
    /// import so static meshes don't import an Avatar/Animator. Only touches packs known to be
    /// static; animated packs (Quaternius animals/fish, UAL, Mixamo) are deliberately excluded.
    /// Re-runnable and idempotent.
    /// </summary>
    public static class StaticPropImportFixer
    {
        private static readonly string[] StaticFolders =
        {
            "Assets/kenney_food-kit",
            "Assets/Textured Stylized Trees - May 2020",
            "Assets/Farm Buildings by Quaternius",
            "Assets/blender",
        };

        [MenuItem("Market/Debug/Fix Static Prop Imports (Rig=None)")]
        public static void FixStaticImports()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", StaticFolders);
            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                        continue;

                    bool needsChange = importer.animationType != ModelImporterAnimationType.None
                                       || importer.importAnimation;
                    if (!needsChange)
                        continue;

                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[StaticPropImportFixer] Done. Updated {changed} of {guids.Length} model(s).");
        }
    }
}
