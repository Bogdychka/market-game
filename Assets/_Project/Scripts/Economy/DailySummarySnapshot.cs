namespace Market.Economy
{
    /// <summary>
    /// Immutable read model for the evening summary UI.
    /// </summary>
    public readonly struct DailySummarySnapshot
    {
        public readonly int Day;
        public readonly float Revenue;
        public readonly float Expenses;
        public readonly float Profit;
        public readonly int ItemsSold;
        public readonly int OrdersCompleted;
        public readonly ItemSO BestSellingItem;
        public readonly int BestSellingQuantity;
        public readonly float BestSellingRevenue;

        public DailySummarySnapshot(
            int day,
            float revenue,
            float expenses,
            float profit,
            int itemsSold,
            int ordersCompleted,
            ItemSO bestSellingItem,
            int bestSellingQuantity,
            float bestSellingRevenue)
        {
            Day = day;
            Revenue = revenue;
            Expenses = expenses;
            Profit = profit;
            ItemsSold = itemsSold;
            OrdersCompleted = ordersCompleted;
            BestSellingItem = bestSellingItem;
            BestSellingQuantity = bestSellingQuantity;
            BestSellingRevenue = bestSellingRevenue;
        }
    }
}
