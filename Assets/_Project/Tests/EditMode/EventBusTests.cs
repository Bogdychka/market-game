using System;
using Market.Core.Events;
using NUnit.Framework;

namespace Market.Tests
{
    /// <summary>
    /// EventBus delivery guarantees: subscribe/unsubscribe bookkeeping and per-handler
    /// exception isolation (audit M2 - one throwing subscriber must not starve the rest).
    /// </summary>
    public class EventBusTests
    {
        private struct PingEvent : IGameEvent { }

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            var bus = new EventBus();
            int count = 0;
            bus.Subscribe<PingEvent>(_ => count++);

            bus.Publish(new PingEvent());

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Publish_ThrowingHandler_DoesNotStopOthers()
        {
            var bus = new EventBus();
            bool secondRan = false;

            bus.Subscribe<PingEvent>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<PingEvent>(_ => secondRan = true);

            // The throwing handler is logged (Debug.LogException) but must not prevent delivery
            // to the second subscriber.
            LogAssert_ExpectException();
            bus.Publish(new PingEvent());

            Assert.IsTrue(secondRan, "Second handler must still run after the first one throws.");
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            Action<PingEvent> handler = _ => count++;

            bus.Subscribe(handler);
            bus.Unsubscribe(handler);
            bus.Publish(new PingEvent());

            Assert.AreEqual(0, count);
        }

        private static void LogAssert_ExpectException()
        {
            // EventBus logs the swallowed handler exception via Debug.LogException; tell the test
            // runner to expect it so the logged error does not fail the test.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
        }
    }
}
