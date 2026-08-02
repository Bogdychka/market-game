using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Applies (or reverts) the Enter Play Mode Options that keep the MCP bridge alive across the
    /// Play Mode transition.
    ///
    /// With a domain reload, entering Play Mode destroys the MCP server instance and its socket,
    /// so the bridge is unreachable for ~5 s (measured) - the Editor also closes every client with
    /// code 4001 first. Disabling the domain reload removes both: the server object survives and
    /// nothing is closed, at the cost of statics no longer resetting between sessions. Everything
    /// this project keeps in statics resets itself at
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> - see <c>ServiceLocator</c>,
    /// <c>FileLogger</c>, <c>GameBootstrap</c> and <c>GrassTrample</c>. New static state must do
    /// the same or it will leak from one Play session into the next.
    ///
    /// The setting lives in <c>ProjectSettings/EditorSettings.asset</c>; these menu items exist
    /// because the running Editor keeps it in memory and would overwrite a hand-edited file.
    /// </summary>
    public static class PlayModeBridgeSetup
    {
        [MenuItem("Market/Debug/MCP/Enable Fast Play Mode (no domain reload)")]
        public static void EnableFastPlayMode()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
            LogState("Fast Play Mode enabled");
        }

        [MenuItem("Market/Debug/MCP/Restore Domain Reload On Play")]
        public static void RestoreDomainReload()
        {
            EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
            LogState("Domain reload on Play restored");
        }

        [MenuItem("Market/Debug/MCP/Log Play Mode Options")]
        public static void LogPlayModeOptions()
        {
            LogState("Current state");
        }

        private static void LogState(string prefix)
        {
            bool reloadsDomain = !EditorSettings.enterPlayModeOptionsEnabled
                || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;

            Debug.Log(
                $"[PlayModeBridgeSetup] {prefix}: optionsEnabled={EditorSettings.enterPlayModeOptionsEnabled}, " +
                $"options={EditorSettings.enterPlayModeOptions}, domainReloadOnPlay={reloadsDomain}");
        }
    }
}
