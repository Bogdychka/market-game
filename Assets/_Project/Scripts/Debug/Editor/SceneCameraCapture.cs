using System.IO;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Editor-only helper that renders the active scene's camera to a PNG on disk so an
    /// off-Editor agent (MCP) can inspect a scene visually without entering Play Mode or
    /// needing the first-person controller. Output lands under the git-ignored
    /// <c>Artifacts/Capture/</c> folder. Temporary debug tooling (see AGENTS.md).
    /// </summary>
    public static class SceneCameraCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Market/Debug/Capture Active Scene Camera")]
        public static void Capture()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                {
                    if (c.isActiveAndEnabled)
                    {
                        cam = c;
                        break;
                    }
                }
            }

            if (cam == null)
            {
                Debug.LogError("SceneCameraCapture: no active camera in the scene.");
                return;
            }

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "Capture");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "scene_camera.png");

            // HDR target: the project renders HDR and tonemaps in post, so an LDR ARGB32 target
            // clips bright scenes to white and misrepresents anything with a strong sun or bloom.
            // No MSAA - the PC renderer is Deferred, where it does nothing (AGENTS.md).
            RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply(false);

                File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log($"SceneCameraCapture: wrote {Width}x{Height} frame from '{cam.name}' to {path}");
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(shot);
            }
        }
    }
}
