namespace Market.World
{
    /// <summary>
    /// Время года. Порядок отвечает смене сезонов: Весна → Лето → Осень → Зима.
    /// Используется SeasonManager, ItemSO (AvailableInSeasons), SupplierShop.
    /// </summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }
}
