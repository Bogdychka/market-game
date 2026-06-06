using Market.Economy;
using TMPro;
using UnityEngine;

namespace Market.UI
{
    /// <summary>
    /// Текстовый HUD текущей суммы денег. Подписывается на MoneySystem.OnChanged.
    /// </summary>
    public class MoneyHUD : MonoBehaviour
    {
        [SerializeField] private MoneySystem moneySystem;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "{0} монет";

        private void Awake()
        {
            if (moneySystem == null) Debug.LogError("[MoneyHUD] moneySystem не назначен", this);
            if (label       == null) Debug.LogError("[MoneyHUD] label не назначен",       this);
        }

        // Стартовый Refresh в Start — после Awake'а MoneySystem (где он инициализирует _amount).
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

        private void Refresh(float amount)
        {
            if (label == null) return;
            label.text = string.Format(format, Mathf.FloorToInt(amount));
        }
    }
}
