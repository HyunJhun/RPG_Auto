using System;
using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>인벤토리 한 칸 (Prefab: UI_InventorySlot)</summary>
    [RequireComponent(typeof(Button))]
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text countText;
        [SerializeField] private Text quickSlotMark;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Color emptyColor = new(0.1f, 0.1f, 0.12f, 0.6f);

        public ItemData Item { get; private set; }
        public event Action<InventorySlotView> OnClicked;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke(this));

        public void Bind(ItemData item, int count, bool selected, string quickSlotKey)
        {
            Item = item;
            bool has = item != null;
            if (icon != null) icon.color = has ? item.SlotColor : emptyColor;
            if (label != null) label.text = has ? item.ShortLabel : string.Empty;
            if (countText != null) countText.text = has && count > 1 ? count.ToString("N0") : string.Empty;
            if (quickSlotMark != null) quickSlotMark.text = has && !string.IsNullOrEmpty(quickSlotKey) ? "Q" + quickSlotKey : string.Empty;
            if (selectedFrame != null) selectedFrame.SetActive(has && selected);
        }
    }
}
