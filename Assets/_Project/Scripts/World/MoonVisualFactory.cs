using UnityEngine;
using UnityEngine.Rendering;

namespace Market.World
{
    /// <summary>
    /// Factory for creating a visual moon disc.
    /// Creates a sphere with bright emission that is independent of scene lighting.
    /// </summary>
    public static class MoonVisualFactory
    {
        /// <summary>
        /// Creates a GameObject with a configured material for rendering the moon.
        /// </summary>
        public static GameObject CreateMoonSphere(float size)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MoonVisual (Auto)";
            sphere.transform.localScale = Vector3.one * size;

            // Collider not needed — this is a decoration only
            if (sphere.TryGetComponent<Collider>(out var col))
                Object.Destroy(col);

            ConfigureRenderer(sphere.GetComponent<Renderer>());
            return sphere;
        }

        private static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode    = ShadowCastingMode.Off;
            renderer.receiveShadows       = false;
            renderer.lightProbeUsage      = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            renderer.material = CreateMoonMaterial();
        }

        /// <summary>
        /// Material with strong emission — moon stays visible regardless of lighting and tone-mapping.
        /// </summary>
        private static Material CreateMoonMaterial()
        {
            // Lit + emission works in both Forward and Deferred URP modes
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[MoonVisualFactory] No suitable shader found!");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var mat = new Material(shader) { name = "MoonVisual Material" };

            var baseColor     = new Color(0.95f, 0.92f, 0.85f, 1f);
            var emissionColor = new Color(3f,    2.8f,  2.5f);

            // URP / Built-in compatibility: set both property names
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     baseColor);
            mat.color = baseColor;

            // URP Lit multiplies _BaseColor by _BaseMap — without a texture the result is black
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);

            // Emission is the primary source of visibility
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            return mat;
        }
    }
}
