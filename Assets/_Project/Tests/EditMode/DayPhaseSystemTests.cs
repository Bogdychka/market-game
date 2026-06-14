using Market.Core;
using Market.Core.Events;
using NUnit.Framework;

namespace Market.Tests
{
    /// <summary>
    /// Guards the visible day rhythm phases used by HUD, opening flow, and later end-day systems.
    /// </summary>
    public class DayPhaseSystemTests
    {
        [TestCase(7, DayPhase.NightNextDay)]
        [TestCase(8, DayPhase.MorningPrep)]
        [TestCase(9, DayPhase.MarketOpen)]
        [TestCase(17, DayPhase.MarketOpen)]
        [TestCase(18, DayPhase.EveningSummary)]
        [TestCase(20, DayPhase.EveningSummary)]
        [TestCase(21, DayPhase.NightNextDay)]
        [TestCase(25, DayPhase.NightNextDay)]
        public void GetPhaseForHour_UsesExpectedBoundaries(int hour, DayPhase expected)
        {
            Assert.AreEqual(expected, DayPhaseSystem.GetPhaseForHour(hour));
        }

        [Test]
        public void SkipHours_AdvancesThroughPhases()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);

            Assert.AreEqual(DayPhase.MorningPrep, phaseSystem.Phase);

            timeSystem.SkipHours(1);
            Assert.AreEqual(DayPhase.MarketOpen, phaseSystem.Phase);

            timeSystem.SkipHours(9);
            Assert.AreEqual(DayPhase.EveningSummary, phaseSystem.Phase);

            timeSystem.SkipHours(3);
            Assert.AreEqual(DayPhase.NightNextDay, phaseSystem.Phase);
        }

        [Test]
        public void PhaseChange_PublishesEventBusEvent()
        {
            var timeSystem = new TimeSystem();
            var eventBus = new EventBus();
            DayPhase received = (DayPhase)(-1);
            eventBus.Subscribe<DayPhaseChangedEvent>(evt => received = evt.NewPhase);

            using var phaseSystem = new DayPhaseSystem(timeSystem, eventBus);
            timeSystem.SkipHours(1);

            Assert.AreEqual(DayPhase.MarketOpen, received);
        }

        [Test]
        public void Reset_ReturnsPhaseToMorningPrep()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);

            timeSystem.SetTime(1, 21, 0);
            Assert.AreEqual(DayPhase.NightNextDay, phaseSystem.Phase);

            timeSystem.Reset();
            Assert.AreEqual(DayPhase.MorningPrep, phaseSystem.Phase);
        }

        [Test]
        public void Midnight_WaitsForSleepBeforeAdvancingDay()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);

            timeSystem.SetTime(3, 23, 0);
            timeSystem.SkipHours(1);

            Assert.AreEqual(3, timeSystem.Day);
            Assert.AreEqual(0, timeSystem.Hour);
            Assert.AreEqual(0, timeSystem.Minute);
            Assert.IsTrue(timeSystem.IsWaitingForSleep);
            Assert.AreEqual(DayPhase.NightNextDay, phaseSystem.Phase);

            Assert.IsTrue(timeSystem.SleepToNextDay());

            Assert.AreEqual(4, timeSystem.Day);
            Assert.AreEqual(8, timeSystem.Hour);
            Assert.AreEqual(DayPhase.MorningPrep, phaseSystem.Phase);
        }
    }
}
