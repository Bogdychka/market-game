using Market.Economy;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Text HUD showing the current money amount. Subscribes to MoneySystem.OnChanged.
    /// </summary>
    public class MoneyHUD : MonoBehaviour
    {
        [SerializeField] private MoneySystem moneySystem;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "{0} coins";

        private void Awake()
        {
            if (moneySystem == null) Debug.LogError("[MoneyHUD] moneySystem not assigned", this);
            if (label       == null) Debug.LogError("[MoneyHUD] label not assigned",       this);
        }

        // Initial refresh in Start -- after MoneySystem.Awake initialises coins.
        private void Start()
        {
            if (moneySystem != null) Refresh(moneySystem.Amount);
        }

        private void OnEnable()
        {
            if (moneySystem != null) moneySystem.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (moneySystem != null) moneySystem.OnChanged -= Refresh;
        }

        private void Refresh(int amount)
        {
            if (label == null) return;
            label.text = string.Format(format, amount);
        }
    }
}
