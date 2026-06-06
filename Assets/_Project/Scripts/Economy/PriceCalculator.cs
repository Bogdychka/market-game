namespace Market.Economy
{
    /// <summary>
    /// Единая прозрачная точка чтения базовых цен товара.
    /// Никакой скрытой математики: поставщик и прилавок используют цены из <see cref="ItemSO"/>.
    /// </summary>
    public class PriceCalculator
    {
        /// <summary>Цена покупки у поставщика.</summary>
        public float GetBuyPrice(ItemSO item) => item != null ? item.BaseBuyPrice : 0f;

        /// <summary>Рекомендованная цена продажи на прилавке.</summary>
        public float GetSuggestedSellPrice(ItemSO item) => item != null ? item.BaseSellPrice : 0f;
    }
}
