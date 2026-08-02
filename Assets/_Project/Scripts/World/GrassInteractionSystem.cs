using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Pushes every registered <see cref="GrassTrample"/> entry (player, NPCs) into the shader
    /// globals GrassWind.shader reads, so grass bends in each mover's travel direction as they walk
    /// through it. One instance per scene that contains GrassWind-shaded grass.
    /// </summary>
    public class GrassInteractionSystem : MonoBehaviour
    {
        private const int MaxInteractors = 16;

        private static readonly int InteractorsId = Shader.PropertyToID("_GrassInteractors");
        private static readonly int DirectionsId = Shader.PropertyToID("_GrassInteractorDirs");
        private static readonly int CountId = Shader.PropertyToID("_GrassInteractorCount");
        private static readonly int BendId = Shader.PropertyToID("_GrassInteractionBend");

        private readonly Vector4[] _positions = new Vector4[MaxInteractors];
        private readonly Vector4[] _directions = new Vector4[MaxInteractors];

        private void LateUpdate()
        {
            GrassTrample.Tick(Time.deltaTime);
            int count = CollectInteractors();
            PushShaderGlobals(count);
        }

        private int CollectInteractors()
        {
            int count = 0;
            for (int i = 0; i < MaxInteractors; i++)
            {
                if (!GrassTrample.TryGet(i, out Vector3 position, out float radius, out Vector2 moveDir, out float speedFactor))
                    break;

                _positions[count] = new Vector4(position.x, position.y, position.z, radius);
                _directions[count] = new Vector4(moveDir.x, moveDir.y, speedFactor, 0f);
                count++;
            }

            return count;
        }

        private void PushShaderGlobals(int count)
        {
            Shader.SetGlobalVectorArray(InteractorsId, _positions);
            Shader.SetGlobalVectorArray(DirectionsId, _directions);
            Shader.SetGlobalInt(CountId, count);
            Shader.SetGlobalFloat(BendId, 0.24f);
        }
    }
}
