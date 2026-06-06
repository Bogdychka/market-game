using Market.Economy;
using Market.World;

namespace Market.Core.Events
{
    // Общие игровые события — добавляются по мере роста проекта.
    // Каждое — struct для дешёвых аллокаций.

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
}
