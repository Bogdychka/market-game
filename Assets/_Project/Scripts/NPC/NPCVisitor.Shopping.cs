using System.Collections.Generic;
using Market.Core.Events;
using Market.Economy;
using Market.Market;
using UnityEngine;

namespace Market.NPC
{
    public partial class NPCVisitor
    {
        private bool TryBuy()
        {
            if (TryBuyPreferredItem(out string failureReason))
                return true;

            Debug.Log($"[NPC] Did not buy at {GetStallLabel(targetStall)}: {failureReason}.");
            return false;
        }

        /// <summary>
        /// Iterates slots and buys the first item of a preferred category within budget.
        /// </summary>
        private bool TryBuyPreferredItem(out string failureReason)
        {
            failureReason = "no items";
            if (targetStall == null || targetStall.Slots == null)
            {
                failureReason = "missing stall";
                return false;
            }

            var slots = targetStall.Slots;
            bool hasStock = false;
            bool hasInterestingCategory = false;
            bool hasOverBudgetItem = false;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!slot.IsOccupied) continue;

                hasStock = true;
                if (!IsPreferredCategory(slot.Item.Category)) continue;

                hasInterestingCategory = true;
                if (slot.SellPrice > maxAcceptablePrice)
                {
                    hasOverBudgetItem = true;
                    continue;
                }

                if (!targetStall.TakeSale(i, out var item, out float price)) continue;

                CompletePurchase(item, price);
                return true;
            }

            failureReason = BuildFailureReason(hasStock, hasInterestingCategory, hasOverBudgetItem);
            return false;
        }

        private bool IsPreferredCategory(ItemCategory category)
        {
            if (_preferredCategories == null || _preferredCategories.Length == 0)
                return true;

            for (int i = 0; i < _preferredCategories.Length; i++)
                if (_preferredCategories[i] == category)
                    return true;

            return false;
        }

        private void CompletePurchase(ItemSO item, float price)
        {
            playerMoney.Add(price);
            _eventBus?.Publish(new ItemSoldEvent(item, price));
            Debug.Log($"[NPC] Bought {item.DisplayName} for {price}. Player funds: {playerMoney.Amount}");
        }

        private string BuildFailureReason(bool hasStock, bool hasInterestingCategory, bool hasOverBudgetItem)
        {
            if (!hasStock) return "no items";
            if (!hasInterestingCategory) return "uninteresting category";
            if (hasOverBudgetItem) return $"over budget (budget {maxAcceptablePrice:0.##})";
            return "no deal available";
        }

        private MarketStall SelectInitialStall()
        {
            return stallRegistry != null ? stallRegistry.GetRandomStall() : targetStall;
        }

        private bool TrySelectNextStall()
        {
            MarketStall nextStall = SelectNextStall();
            if (nextStall == null)
                return false;

            targetStall = nextStall;
            Debug.Log($"[NPC] Browsing next stall: {GetStallLabel(targetStall)}.");
            return true;
        }

        private MarketStall SelectNextStall()
        {
            if (stallRegistry == null || stallRegistry.Count == 0)
                return null;

            MarketStall fallback = null;
            IReadOnlyList<MarketStall> stalls = stallRegistry.Stalls;
            for (int i = 0; i < stalls.Count; i++)
            {
                MarketStall stall = stalls[i];
                if (!CanVisitStall(stall)) continue;

                fallback ??= stall;
                if (HasStock(stall))
                    return stall;
            }

            return fallback;
        }

        private bool CanVisitStall(MarketStall stall)
        {
            return stall != null
                   && stall != targetStall
                   && !_visitedStalls.Contains(stall);
        }

        private bool HasStock(MarketStall stall)
        {
            if (stall == null || stall.Slots == null)
                return false;

            var slots = stall.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsOccupied)
                    return true;
            }

            return false;
        }

        private void RememberVisitedStall(MarketStall stall)
        {
            if (stall != null && !_visitedStalls.Contains(stall))
                _visitedStalls.Add(stall);
        }

        private string GetStallLabel(MarketStall stall)
        {
            return stall != null ? stall.StallId : "missing stall";
        }
    }
}
