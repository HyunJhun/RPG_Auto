using System;
using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>상점 목록 한 줄 (Prefab: UI_ShopRow)</summary>
    [RequireComponent(typeof(Button))]
    public class ShopRowView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text subText;
        [SerializeField] private Text priceText;
        [SerializeField] private GameObject selectedFrame;

        public int Index { get; set; }
        public event Action<ShopRowView> OnClicked;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke(this));

        public void Bind(ItemData item, string sub, long price, bool selected)
        {
            if (icon != null) icon.color = item.SlotColor;
            if (iconLabel != null) iconLabel.text = item.ShortLabel;
            if (nameText != null) nameText.text = item.DisplayName;
            if (subText != null) subText.text = sub;
            if (priceText != null) priceText.text = $"{price:N0} G";
            if (selectedFrame != null) selectedFrame.SetActive(selected);
        }
    }
}
