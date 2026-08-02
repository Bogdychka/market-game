using Market.World;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Shared editor plumbing for wave profiles: pushes an edited profile straight to the binders
    /// in the open scene so the Scene view shows the change while it is being made, instead of
    /// only after entering Play Mode.
    /// </summary>
    public static class WaveProfileEditorUtility
    {
        /// <summary>
        /// Re-uploads <paramref name="profile"/> through every binder in the open scenes that uses
        /// it and repaints the Scene view.
        /// </summary>
        public static void PushToScene(WaveProfile profile)
        {
            if (profile == null)
                return;

            WaveProfileBinder[] binders =
                Object.FindObjectsByType<WaveProfileBinder>(FindObjectsInactive.Exclude);

            for (int i = 0; i < binders.Length; i++)
            {
                if (binders[i] != null && binders[i].Profile == profile)
                    binders[i].UploadProfile();
            }

            if (binders.Length == 0)
            {
                // No binder in the scene: upload directly so a lab scene still previews the bank.
                WaveShaderBridge.Upload(profile);
            }

            SceneView.RepaintAll();
        }
    }
}
