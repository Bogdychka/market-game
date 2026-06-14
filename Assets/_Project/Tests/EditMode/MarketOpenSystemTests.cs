using Market.Core;
using Market.Core.Events;
using NUnit.Framework;

namespace Market.Tests
{
    /// <summary>
    /// Guards the explicit player-controlled open/close state for customer shopping.
    /// </summary>
    public class MarketOpenSystemTests
    {
        [Test]
        public void Market_StartsClosed_AndCanOpenDuringMorningOrMarketHours()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);
            using var marketOpenSystem = new MarketOpenSystem(phaseSystem);

            Assert.IsFalse(marketOpenSystem.IsOpen);
            Assert.IsTrue(marketOpenSystem.TryOpen());
            Assert.IsTrue(marketOpenSystem.IsOpen);

            Assert.IsTrue(marketOpenSystem.TryClose());
            Assert.IsFalse(marketOpenSystem.IsOpen);

            timeSystem.SetTime(1, 18, 0);
            Assert.IsFalse(marketOpenSystem.TryOpen());
        }

        [Test]
        public void NightAndNewMorning_CloseMarket()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);
            using var marketOpenSystem = new MarketOpenSystem(phaseSystem);

            Assert.IsTrue(marketOpenSystem.TryOpen());

            timeSystem.SetTime(1, 21, 0);
            Assert.IsFalse(marketOpenSystem.IsOpen);

            timeSystem.SetTime(1, 9, 0);
            Assert.IsTrue(marketOpenSystem.TryOpen());

            timeSystem.SetTime(2, 8, 0);
            Assert.IsFalse(marketOpenSystem.IsOpen);
        }

        [Test]
        public void OpenChange_PublishesEventBusEvent()
        {
            var timeSystem = new TimeSystem();
            using var phaseSystem = new DayPhaseSystem(timeSystem);
            var eventBus = new EventBus();
            bool received = false;
            eventBus.Subscribe<MarketOpenChangedEvent>(evt => received = evt.IsOpen);

            using var marketOpenSystem = new MarketOpenSystem(phaseSystem, eventBus);
            marketOpenSystem.TryOpen();

            Assert.IsTrue(received);
        }
    }
}
