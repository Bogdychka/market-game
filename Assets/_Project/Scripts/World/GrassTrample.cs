using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Registry of world-space "mover" transforms (player, NPCs) that
    /// <see cref="GrassInteractionSystem"/> pushes into GrassWind's shader globals each frame, so the
    /// wind near each mover blows in the direction they walk and physically yields around their
    /// body. Direction and engagement are smoothed over time here (the shader is stateless): the
    /// heading holds when movement stops, while bend engagement eases back to zero.
    /// </summary>
    public static class GrassTrample
    {
        /// <summary>Planar speed (m/s) above which a mover counts as "walking" and steers the wind.</summary>
        private const float MoveThreshold = 0.05f;

        private const float DirectionSmoothRate = 5f;
        private const float EngageRate = 8f;
        private const float ReleaseRate = 4f;
        private const float FullBendSpeed = 2.2f;

        private sealed class Entry
        {
            public Transform Transform;
            public float Radius;
            public Vector3 LastPosition;
            public Vector2 Direction;   // smoothed heading sent to the shader
            public float Engagement;    // smoothed override weight [0..1]
        }

        private static readonly List<Entry> Active = new();

        public static int Count => Active.Count;

        /// <summary>
        /// Play mode runs without a domain reload, so statics survive between sessions. Anything
        /// left here would hold destroyed transforms from the previous run.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
        }

        /// <summary>Adds a moving transform to the shared grass-interaction feed.</summary>
        public static void Register(Transform transform, float radius)
        {
            Active.Add(new Entry
            {
                Transform = transform,
                Radius = radius,
                LastPosition = transform.position,
                Direction = Vector2.up, // arbitrary; irrelevant until Engagement rises from 0
                Engagement = 0f,
            });
        }

        /// <summary>Removes every interaction entry owned by the supplied transform.</summary>
        public static void Unregister(Transform transform)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i].Transform == transform)
                    Active.RemoveAt(i);
            }
        }

        /// <summary>
        /// Advance each mover's smoothed heading/engagement from its per-frame displacement. While
        /// walking, the heading follows travel and engagement follows speed. While stopped, the
        /// heading holds but engagement releases, so grass rises instead of staying flattened.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float invDt = 1f / deltaTime;
            RemoveMissingEntries();

            for (int i = 0; i < Active.Count; i++)
            {
                Entry e = Active[i];
                Vector3 pos = e.Transform.position;
                Vector3 delta = pos - e.LastPosition;
                e.LastPosition = pos;

                Vector2 planarVelocity = new Vector2(delta.x, delta.z) * invDt;
                float speed = planarVelocity.magnitude;
                if (speed <= MoveThreshold)
                {
                    e.Engagement = Damp(e.Engagement, 0f, ReleaseRate, deltaTime);
                    continue;
                }

                Vector2 targetDir = planarVelocity / speed;
                float directionT = 1f - Mathf.Exp(-deltaTime * DirectionSmoothRate);
                Vector3 cur = new Vector3(e.Direction.x, 0f, e.Direction.y);
                Vector3 tgt = new Vector3(targetDir.x, 0f, targetDir.y);
                Vector3 blended = Vector3.Slerp(cur, tgt, directionT);
                Vector2 planar = new Vector2(blended.x, blended.z);
                e.Direction = planar.sqrMagnitude > 1e-6f ? planar.normalized : targetDir;

                float targetEngagement = Mathf.Clamp01(speed / FullBendSpeed);
                e.Engagement = Damp(e.Engagement, targetEngagement, EngageRate, deltaTime);
            }
        }

        private static float Damp(float current, float target, float rate, float deltaTime)
        {
            float t = 1f - Mathf.Exp(-deltaTime * rate);
            return Mathf.Lerp(current, target, t);
        }

        private static void RemoveMissingEntries()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i].Transform == null)
                    Active.RemoveAt(i);
            }
        }

        /// <summary>Reads one compact shader-ready interaction entry by registry index.</summary>
        public static bool TryGet(int index, out Vector3 position, out float radius, out Vector2 moveDir, out float engagement)
        {
            if (index < 0 || index >= Active.Count || Active[index].Transform == null)
            {
                position = default;
                radius = default;
                moveDir = default;
                engagement = default;
                return false;
            }

            Entry e = Active[index];
            position = e.Transform.position;
            radius = e.Radius;
            moveDir = e.Direction;
            engagement = e.Engagement;
            return true;
        }
    }
}
