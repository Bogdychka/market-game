#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot maintenance tool. The NPC Animator (NPC_Anim.controller) plays Idle/Walk/Talk clips
    /// that live inside UAL1_Standard.fbx. Those clips are imported with Loop Time off by default, so
    /// each plays once and then freezes on its last frame (the NPC "walks then locks up and slides").
    /// This re-imports the FBX keeping only the three clips the controller references, with Loop Time
    /// enabled. Clip fileIDs are derived from the clip name, so keeping the names preserves the
    /// controller's motion references.
    /// </summary>
    internal static class NpcAnimationLoopFixer
    {
        private const string UalPath =
            "Assets/Universal Animation Library[Standard]/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx";

        // fileIDs referenced by NPC_Anim.controller: Idle + Walk (blend tree children) and Talk (state).
        private static readonly long[] UsedFileIds =
        {
            7994627924475153551L,   // Idle
            -7003542030617561338L,  // Walk
            6476497563760123848L,   // Talk
        };

        [MenuItem("Market/Debug/Fix NPC Animation Loops")]
        private static void Fix()
        {
            var importer = AssetImporter.GetAtPath(UalPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[NpcAnimationLoopFixer] ModelImporter not found at {UalPath}");
                return;
            }

            // Map each imported clip name to its persistent fileID so we can match the controller refs.
            var fileIdByName = new Dictionary<string, long>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(UalPath))
            {
                if (asset is AnimationClip clip &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string _, out long fileId))
                {
                    fileIdByName[clip.name] = fileId;
                }
            }

            var kept = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation def in importer.defaultClipAnimations)
            {
                if (!fileIdByName.TryGetValue(def.name, out long fileId)) continue;
                if (System.Array.IndexOf(UsedFileIds, fileId) < 0) continue;

                def.loopTime = true;
                def.loopPose = true;
                kept.Add(def);
                Debug.Log($"[NpcAnimationLoopFixer] Looping '{def.name}' (fileID {fileId}).");
            }

            if (kept.Count == 0)
            {
                Debug.LogError("[NpcAnimationLoopFixer] No clips matched the controller fileIDs -- aborted.");
                return;
            }

            importer.clipAnimations = kept.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[NpcAnimationLoopFixer] Done -- {kept.Count} clip(s) set to loop and re-imported.");
        }
    }
}
#endif
