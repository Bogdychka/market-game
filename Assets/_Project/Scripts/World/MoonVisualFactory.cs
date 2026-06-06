using UnityEngine;
using UnityEngine.Rendering;

namespace Market.World
{
    /// <summary>
    /// Фабрика для создания визуального диска луны.
    /// Создаёт сферу с яркой эмиссией, не зависящей от освещения сцены.
    /// </summary>
    public static class MoonVisualFactory
    {
        /// <summary>
        /// Создаёт GameObject с настроенным материалом для отображения луны.
        /// </summary>
        public static GameObject CreateMoonSphere(float size)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MoonVisual (Auto)";
            sphere.transform.localScale = Vector3.one * size;

            // Коллайдер не нужен — это декорация
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
        /// Материал с сильной эмиссией — луна видна независимо от освещения и тонмаппинга.
        /// </summary>
        private static Material CreateMoonMaterial()
        {
            // Lit + emission работает в Forward и Deferred режимах URP
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[MoonVisualFactory] Не найден ни один подходящий шейдер!");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var mat = new Material(shader) { name = "MoonVisual Material" };

            var baseColor     = new Color(0.95f, 0.92f, 0.85f, 1f);
            var emissionColor = new Color(3f,    2.8f,  2.5f);

            // URP / Built-in совместимость: ставим оба имени свойств
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     baseColor);
            mat.color = baseColor;

            // URP Lit умножает _BaseColor на _BaseMap — без текстуры будет чёрное
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);

            // Эмиссия — главный источник видимости
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
