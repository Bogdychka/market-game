using Market.Economy;

namespace Market.UI
{
    /// <summary>
    /// Fills the shared market panel with the player's inventory: one info row per item.
    /// Stateless between refreshes; reflects the Inventory model only.
    /// </summary>
    public class InventoryPanelRenderer
    {
        private readonly MarketPanelView _view;

        public InventoryPanelRenderer(MarketPanelView view)
        {
            _view = view;
        }

        /// <summary>Render the inventory contents into the cleared panel.</summary>
        public void Render(Inventory inventory, string subtitle)
        {
            _view.SetHeader("Inventory", subtitle);

            if (inventory == null || inventory.Items.Count == 0)
            {
                _view.CreateEmptyText("Inventory is empty.");
                return;
            }

            foreach (var entry in inventory.Items)
            {
                ItemSO item = entry.Key;
                int count = entry.Value;
                if (item == null) continue;

                _view.CreateInfoRow(item, item.DisplayName, $"x{count}", item.Category.ToString());
            }
        }
    }
}
