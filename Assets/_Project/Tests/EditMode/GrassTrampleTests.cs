using Market.World;
using NUnit.Framework;
using UnityEngine;

namespace Market.Tests
{
    public class GrassTrampleTests
    {
        [Test]
        public void Engagement_ReleasesAfterMoverStops()
        {
            var mover = new GameObject("GrassTrampleTestMover");
            int index = GrassTrample.Count;
            GrassTrample.Register(mover.transform, 0.6f);

            try
            {
                mover.transform.position = Vector3.right;
                GrassTrample.Tick(1f);
                Assert.That(
                    GrassTrample.TryGet(index, out _, out _, out Vector2 direction, out float moving),
                    Is.True);
                Assert.That(direction.x, Is.GreaterThan(0.9f));
                Assert.That(moving, Is.GreaterThan(0.4f));

                GrassTrample.Tick(1f);
                Assert.That(GrassTrample.TryGet(index, out _, out _, out _, out float stopped), Is.True);
                Assert.That(stopped, Is.LessThan(moving * 0.1f));
            }
            finally
            {
                GrassTrample.Unregister(mover.transform);
                Object.DestroyImmediate(mover);
            }
        }
    }
}
