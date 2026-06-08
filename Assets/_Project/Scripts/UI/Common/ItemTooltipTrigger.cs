using System;
using Market.Economy;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Market.UI
{
    /// <summary>
    /// Attach to a UI row to fire enter/exit callbacks for item hover tooltips.
    /// Call <see cref="Setup"/> immediately after AddComponent.
    /// </summary>
    public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ItemSO         _item;
        private Action<ItemSO> _onEnter;
        private Action         _onExit;

        /// <summary>Bind the item and the show/hide callbacks.</summary>
        public void Setup(ItemSO item, Action<ItemSO> onEnter, Action onExit)
        {
            _item    = item;
            _onEnter = onEnter;
            _onExit  = onExit;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke(_item);
        public void OnPointerExit(PointerEventData eventData)  => _onExit?.Invoke();
    }
}
