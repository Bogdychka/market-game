using Market.DebugTools;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Ensures temporary debug components are present on the Player prefab.
    /// </summary>
    public static class PlayerDebugToolsInstaller
    {
        private const string PlayerPrefabPath = "Assets/_Project/Art/Prefabs/Player/Player.prefab";

        [MenuItem("Market/Debug/Add Fly Mode To Player")]
        public static void AddFlyModeToPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<DebugFlyMode>() == null)
                    root.AddComponent<DebugFlyMode>();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[PlayerDebugToolsInstaller] DebugFlyMode ensured on Player prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
