namespace Market.Economy
{
    /// <summary>
    /// Single transparent price-read point.
    /// No hidden math: supplier and stall both read prices from <see cref="ItemSO"/>.
    /// </summary>
    public class PriceCalculator
    {
        /// <summary>Purchase price at the supplier.</summary>
        public float GetBuyPrice(ItemSO item) => item != null ? item.BaseBuyPrice : 0f;

        /// <summary>Suggested sell price at the player stall.</summary>
        public float GetSuggestedSellPrice(ItemSO item) => item != null ? item.BaseSellPrice : 0f;
    }
}
