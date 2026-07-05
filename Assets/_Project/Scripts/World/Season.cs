namespace Market.World
{
    /// <summary>
    /// Season enum. Order matches the seasonal cycle: Spring -> Summer -> Autumn -> Winter.
    /// Used by SeasonManager, ItemSO (AvailableInSeasons), and SupplierShop.
    /// </summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }
}
