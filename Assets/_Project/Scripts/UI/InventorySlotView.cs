using System;
using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 아이템 칸 (Prefab: UI_InventorySlot). 인벤토리·장비창 공용.
    /// 등급 배경색, Lv(우상단), +강화(좌하단), 수량(우하단), E 장착 배지(좌상단), 퀵슬롯 표시.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text levelText;
        [SerializeField] private Text enhanceText;
        [SerializeField] private Text countText;
        [SerializeField] private Text quickSlotMark;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Text placeholderText;

        [Header("Style")]
        [SerializeField] private Color emptyColor = new(0.1f, 0.1f, 0.12f, 0.85f);
        [SerializeField] private Color levelOkColor = new(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color levelLowColor = new(1f, 0.35f, 0.3f);

        public InventoryEntry Entry { get; private set; }
        public EquipmentInstance Equipment { get; private set; }
        public bool IsEmpty => Entry == null && Equipment == null;

        public event Action<InventorySlotView> OnClicked;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke(this));

        /// <summary>인벤토리 칸</summary>
        public void Bind(InventoryEntry entry, bool selected, bool equipped, string quickKey, bool levelOk)
        {
            Entry = entry;
            Equipment = entry?.Equipment;
            if (entry == null)
            {
                Clear(null);
                return;
            }
            Show(entry.Item, entry.Equipment, entry.Item.IsStackable ? entry.Count : 1, selected, equipped, quickKey, levelOk);
        }

        /// <summary>장비창 칸 (장착 중인 장비 또는 빈 칸 + 부위 이름)</summary>
        public void BindEquipment(EquipmentInstance equipment, string placeholder, bool selected)
        {
            Entry = null;
            Equipment = equipment;
            if (equipment == null)
            {
                Clear(placeholder);
                return;
            }
            Show(equipment.Data, equipment, 1, selected, false, null, true);
        }

        public void Clear(string placeholder)
        {
            Entry = null;
            Equipment = null;
            if (background != null) background.color = emptyColor;
            if (icon != null) icon.enabled = false;
            SetText(label, null);
            SetText(levelText, null);
            SetText(enhanceText, null);
            SetText(countText, null);
            SetText(quickSlotMark, null);
            SetText(placeholderText, placeholder);
            if (equippedBadge != null) equippedBadge.SetActive(false);
            if (selectedFrame != null) selectedFrame.SetActive(false);
        }

        private void Show(ItemData item, EquipmentInstance eq, int count, bool selected, bool equipped, string quickKey, bool levelOk)
        {
            if (background != null) background.color = ItemGradeColors.Background(item.Grade);
            if (icon != null)
            {
                icon.enabled = true;
                icon.color = item.SlotColor;
            }
            SetText(label, item.ShortLabel);
            SetText(placeholderText, null);

            if (item is EquipmentData data)
            {
                SetText(levelText, $"Lv.{data.RequiredLevel}");
                if (levelText != null) levelText.color = levelOk ? levelOkColor : levelLowColor;
                SetText(enhanceText, eq != null && eq.EnhanceLevel > 0 ? $"+{eq.EnhanceLevel}" : null);
                SetText(countText, null);
                SetText(quickSlotMark, null);
            }
            else
            {
                SetText(levelText, null);
                SetText(enhanceText, null);
                SetText(countText, count > 1 ? count.ToString("N0") : null);
                SetText(quickSlotMark, string.IsNullOrEmpty(quickKey) ? null : "Q" + quickKey);
            }

            if (equippedBadge != null) equippedBadge.SetActive(equipped);
            if (selectedFrame != null) selectedFrame.SetActive(selected);
        }

        private static void SetText(Text t, string value)
        {
            if (t == null) return;
            bool has = !string.IsNullOrEmpty(value);
            t.text = has ? value : string.Empty;
            t.enabled = has;
        }
    }
}
