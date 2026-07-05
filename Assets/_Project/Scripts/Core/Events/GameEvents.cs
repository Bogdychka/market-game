using Market.Core;
using Market.Economy;
using Market.World;

namespace Market.Core.Events
{
    // Shared game events -- add new ones as the project grows.
    // Each is a struct for cheap allocations.

    public readonly struct MoneyChangedEvent : IGameEvent
    {
        public readonly float Amount;
        public readonly float Delta;
        public MoneyChangedEvent(float amount, float delta) { Amount = amount; Delta = delta; }
    }

    public readonly struct ItemPurchasedEvent : IGameEvent
    {
        public readonly ItemSO Item;
        public readonly float Price;
        public ItemPurchasedEvent(ItemSO item, float price) { Item = item; Price = price; }
    }

    public readonly struct ItemSoldEvent : IGameEvent
    {
        public readonly ItemSO Item;
        public readonly float Price;
        public ItemSoldEvent(ItemSO item, float price) { Item = item; Price = price; }
    }

    public readonly struct SeasonChangedEvent : IGameEvent
    {
        public readonly Season NewSeason;
        public SeasonChangedEvent(Season season) { NewSeason = season; }
    }

    public readonly struct DayPhaseChangedEvent : IGameEvent
    {
        public readonly DayPhase NewPhase;
        public DayPhaseChangedEvent(DayPhase phase) { NewPhase = phase; }
    }

    public readonly struct MarketOpenChangedEvent : IGameEvent
    {
        public readonly bool IsOpen;
        public MarketOpenChangedEvent(bool isOpen) { IsOpen = isOpen; }
    }
}
