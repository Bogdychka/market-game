using Market.Economy;

namespace Market.UI
{
    /// <summary>
    /// Renders the end-of-day business report inside the shared market panel chrome.
    /// </summary>
    public class EveningSummaryPanelRenderer
    {
        private readonly MarketPanelView _view;

        public EveningSummaryPanelRenderer(MarketPanelView view)
        {
            _view = view;
        }

        public void Render(DailySummarySnapshot summary)
        {
            _view.SetHeader("Evening Summary", $"Day {summary.Day} results");
            _view.CreateSectionLabel("Money");
            _view.CreateInfoRow(null, "Revenue", Coins(summary.Revenue), null);
            _view.CreateInfoRow(null, "Expenses", Coins(summary.Expenses), null);
            _view.CreateInfoRow(null, "Profit", Coins(summary.Profit), null);

            _view.CreateSectionLabel("Activity");
            _view.CreateInfoRow(null, "Items sold", summary.ItemsSold.ToString(), null);
            _view.CreateInfoRow(null, "Orders done", summary.OrdersCompleted.ToString(), "Orders arrive in D8");

            _view.CreateSectionLabel("Best seller");
            if (summary.BestSellingItem == null)
            {
                _view.CreateEmptyText("No items sold today");
                return;
            }

            string detail = $"{summary.BestSellingQuantity} sold | {Coins(summary.BestSellingRevenue)} revenue";
            _view.CreateInfoRow(
                summary.BestSellingItem,
                summary.BestSellingItem.DisplayName,
                $"{summary.BestSellingQuantity}x",
                detail);
        }

        private static string Coins(float value)
        {
            return $"{value:0.##} coins";
        }
    }
}
