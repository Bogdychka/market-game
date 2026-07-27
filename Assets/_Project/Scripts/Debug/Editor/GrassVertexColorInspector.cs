using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// One-shot diagnostic: logs per-channel vertex color ranges for the grass meshes so we know
    /// which channel (if any) survived FBX import as the wind mask before wiring a wind shader to it.
    /// </summary>
    public static class GrassVertexColorInspector
    {
        [MenuItem("Market/Debug/Inspect Grass Vertex Colors")]
        public static void Inspect()
        {
            InspectAsset("Assets/blender/Grass_1.fbx");
            InspectAsset("Assets/blender/Grass_2.fbx");
        }

        private static void InspectAsset(string path)
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            if (mesh == null)
            {
                Debug.LogWarning($"[GrassVertexColorInspector] No mesh found in {path}");
                return;
            }

            Color[] colors = mesh.colors;
            if (colors == null || colors.Length == 0)
            {
                Debug.LogWarning($"[GrassVertexColorInspector] {path} ({mesh.name}): mesh has NO vertex colors.");
                return;
            }

            Vector3 min = mesh.bounds.min;
            Vector3 max = mesh.bounds.max;
            float rMin = colors.Min(c => c.r), rMax = colors.Max(c => c.r);
            float gMin = colors.Min(c => c.g), gMax = colors.Max(c => c.g);
            float bMin = colors.Min(c => c.b), bMax = colors.Max(c => c.b);
            float aMin = colors.Min(c => c.a), aMax = colors.Max(c => c.a);

            Debug.Log($"[GrassVertexColorInspector] {path} ({mesh.name}): verts={colors.Length}, " +
                      $"boundsY=[{min.y:0.###},{max.y:0.###}] " +
                      $"R=[{rMin:0.###},{rMax:0.###}] G=[{gMin:0.###},{gMax:0.###}] " +
                      $"B=[{bMin:0.###},{bMax:0.###}] A=[{aMin:0.###},{aMax:0.###}]");

            // Correlate each channel with local Y (root-to-tip axis) to spot which one is a wind mask.
            for (int ch = 0; ch < 4; ch++)
            {
                float sum = 0f;
                for (int i = 0; i < colors.Length; i++)
                {
                    float y01 = max.y > min.y ? Mathf.InverseLerp(min.y, max.y, mesh.vertices[i].y) : 0f;
                    float v = ch switch { 0 => colors[i].r, 1 => colors[i].g, 2 => colors[i].b, _ => colors[i].a };
                    sum += Mathf.Abs(v - y01);
                }
                string channelName = ch switch { 0 => "R", 1 => "G", 2 => "B", _ => "A" };
                Debug.Log($"[GrassVertexColorInspector] {mesh.name} channel {channelName}: avg |value - heightY01| = {sum / colors.Length:0.###} (closer to 0 = tracks height = likely wind mask)");
            }
        }
    }
}
