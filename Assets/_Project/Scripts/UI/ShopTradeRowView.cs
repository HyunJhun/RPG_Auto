using System;
using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>상점 거래 한 줄 (Prefab: UI_ShopTradeRow) — 아이콘, 이름, 가격, 수량 슬라이더</summary>
    public class ShopTradeRowView : MonoBehaviour
    {
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text subText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text quantityText;
        [SerializeField] private Slider slider;
        [SerializeField] private Button removeButton;

        public int Index { get; set; }

        public event Action<ShopTradeRowView, int> OnQuantityChanged;
        public event Action<ShopTradeRowView> OnRemove;

        private void Awake()
        {
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.onValueChanged.AddListener(v =>
                {
                    int q = Mathf.RoundToInt(v);
                    if (quantityText != null) quantityText.text = q.ToString("N0");
                    OnQuantityChanged?.Invoke(this, q);
                });
            }
            removeButton?.onClick.AddListener(() => OnRemove?.Invoke(this));
        }

        public void Bind(ItemData item, string displayName, string sub, long unitPrice, int max, int quantity, bool removable)
        {
            if (iconBackground != null) iconBackground.color = ItemGradeColors.Background(item.Grade);
            if (icon != null) icon.color = item.SlotColor;
            if (iconLabel != null) iconLabel.text = item.ShortLabel;
            if (nameText != null)
            {
                nameText.text = displayName;
                nameText.color = ItemGradeColors.Text(item.Grade);
            }
            if (subText != null) subText.text = sub;
            if (priceText != null) priceText.text = $"{unitPrice:N0} G";
            if (quantityText != null) quantityText.text = quantity.ToString("N0");
            if (slider != null)
            {
                slider.minValue = 0;
                slider.maxValue = Mathf.Max(0, max);
                slider.interactable = max > 0;
                slider.SetValueWithoutNotify(quantity);
            }
            if (removeButton != null) removeButton.gameObject.SetActive(removable);
        }
    }
}
