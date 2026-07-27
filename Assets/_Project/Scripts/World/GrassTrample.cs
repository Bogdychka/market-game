using System.Collections.Generic;
using UnityEngine;

namespace Market.World
{
    /// <summary>
    /// Registry of world-space "mover" transforms (player, NPCs) that
    /// <see cref="GrassInteractionSystem"/> pushes into GrassWind's shader globals each frame, so the
    /// wind near each mover blows in the direction they walk. Direction and engagement are smoothed
    /// over time here (the shader is stateless): the local wind eases toward the travel direction
    /// while moving, and holds that direction indefinitely once stopped, until the mover walks again.
    /// </summary>
    public static class GrassTrample
    {
        /// <summary>Planar speed (m/s) above which a mover counts as "walking" and steers the wind.</summary>
        private const float MoveThreshold = 0.05f;

        /// <summary>Exponential smoothing rate; ~3 reaches ~95% of a change in about one second.</summary>
        private const float SmoothRate = 3f;

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
        /// walking, both ease toward (travel direction, 1). While stopped, both hold their last
        /// value, so the grass keeps the last walked direction until the mover moves again.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float invDt = 1f / deltaTime;
            float t = 1f - Mathf.Exp(-deltaTime * SmoothRate);

            for (int i = 0; i < Active.Count; i++)
            {
                Entry e = Active[i];
                if (e.Transform == null)
                    continue;

                Vector3 pos = e.Transform.position;
                Vector3 delta = pos - e.LastPosition;
                e.LastPosition = pos;

                Vector2 planarVelocity = new Vector2(delta.x, delta.z) * invDt;
                float speed = planarVelocity.magnitude;
                if (speed <= MoveThreshold)
                    continue; // stopped: hold last heading + engagement

                Vector2 targetDir = planarVelocity / speed;

                // Slerp (via 3D) so the heading rotates smoothly the short way round, even on a
                // near-reversal, instead of a straight vector lerp that would dip through zero.
                Vector3 cur = new Vector3(e.Direction.x, 0f, e.Direction.y);
                Vector3 tgt = new Vector3(targetDir.x, 0f, targetDir.y);
                Vector3 blended = Vector3.Slerp(cur, tgt, t);
                Vector2 planar = new Vector2(blended.x, blended.z);
                e.Direction = planar.sqrMagnitude > 1e-6f ? planar.normalized : targetDir;

                e.Engagement = Mathf.Lerp(e.Engagement, 1f, t);
            }
        }

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
