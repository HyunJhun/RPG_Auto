using RPGAuto.Items;
using RPGAuto.QuickSlots;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 아이템 상세 팝업 (인벤토리·장비창 공용).
    /// 소모품: 사용 / 퀵슬롯(7~0) 등록, 장비: 장착·해제 / 강화, 공통: 버리기(수량 선택).
    /// </summary>
    public class ItemDetailPopup : MonoBehaviour, IGameWindow
    {
        /// <summary>팝업이 붙을 위치</summary>
        public enum Dock
        {
            /// <summary>우측 패널(인벤토리)의 왼쪽</summary>
            LeftOfRightPanel,
            /// <summary>좌측 패널(장비창)의 오른쪽</summary>
            RightOfLeftPanel
        }

        [Header("Data")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private QuickSlotBar quickSlots;
        [SerializeField] private EnhancePopup enhancePopup;

        [Header("Window")]
        [SerializeField] private RectTransform window;
        [SerializeField] private Button closeButton;
        [SerializeField] private float panelMargin = 20f;
        [SerializeField] private float rightPanelWidth = 520f;
        [SerializeField] private float leftPanelWidth = 480f;
        [SerializeField] private float gap = 12f;
        [SerializeField] private float verticalOffset = -40f;

        [Header("Parts")]
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text typeText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text messageText;

        [Header("Actions")]
        [SerializeField] private Button useButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Text equipButtonText;
        [SerializeField] private Button enhanceButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private OptionRowView quantityRow;
        [SerializeField] private GameObject quickSlotGroup;
        [SerializeField] private Button[] quickSlotButtons = new Button[QuickSlotBar.ItemSlotCount];

        [Header("Style")]
        [SerializeField] private Color quickSlotNormal = new(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color quickSlotAssigned = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f);
        [SerializeField] private Color okColor = new(0.55f, 0.95f, 0.55f);

        private ItemData _item;
        private EquipmentInstance _equipment;
        private int _quantity = 1;

        public bool IsOpen => window != null && window.gameObject.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => SetOpen(false));
            useButton?.onClick.AddListener(Use);
            equipButton?.onClick.AddListener(ToggleEquip);
            enhanceButton?.onClick.AddListener(() => { if (_equipment != null) enhancePopup?.Open(_equipment); });
            discardButton?.onClick.AddListener(Discard);
            if (quantityRow != null)
            {
                quantityRow.OnMinus += () => { _quantity = Mathf.Max(1, _quantity - 1); Refresh(); };
                quantityRow.OnPlus += () => { _quantity = Mathf.Min(MaxQuantity, _quantity + 1); Refresh(); };
            }
            for (int i = 0; i < quickSlotButtons.Length; i++)
            {
                int index = i; // 클로저 캡처용
                quickSlotButtons[i]?.onClick.AddListener(() => ToggleQuickSlot(index));
            }
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnInventoryChanged += HandleChanged;
            if (equipment != null) equipment.OnEquipmentChanged += HandleChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnInventoryChanged -= HandleChanged;
            if (equipment != null) equipment.OnEquipmentChanged -= HandleChanged;
        }

        private void HandleChanged()
        {
            if (!IsOpen) return;
            // 보고 있던 아이템이 사라졌으면 닫는다
            bool gone = _equipment != null ? !inventory.Contains(_equipment) : inventory.GetCount(_item) <= 0;
            if (gone) SetOpen(false);
            else Refresh();
        }

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.gameObject.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (!open && enhancePopup != null && enhancePopup.IsOpen) enhancePopup.SetOpen(false);
        }

        public void Show(InventoryEntry entry, Dock dock)
        {
            if (entry == null) return;
            Open(entry.Item, entry.Equipment, dock);
        }

        public void ShowEquipment(EquipmentInstance eq, Dock dock)
        {
            if (eq == null) return;
            Open(eq.Data, eq, dock);
        }

        private void Open(ItemData item, EquipmentInstance eq, Dock dock)
        {
            _item = item;
            _equipment = eq;
            _quantity = 1;
            if (messageText != null) messageText.text = string.Empty;
            Place(dock);
            SetOpen(true);
            Refresh();
        }

        private void Place(Dock dock)
        {
            if (dock == Dock.LeftOfRightPanel)
            {
                window.anchorMin = window.anchorMax = new Vector2(1f, 0.5f);
                window.pivot = new Vector2(1f, 0.5f);
                window.anchoredPosition = new Vector2(-(panelMargin + rightPanelWidth + gap), verticalOffset);
            }
            else
            {
                window.anchorMin = window.anchorMax = new Vector2(0f, 0.5f);
                window.pivot = new Vector2(0f, 0.5f);
                window.anchoredPosition = new Vector2(panelMargin + leftPanelWidth + gap, verticalOffset);
            }
        }

        // ---------------- Actions ----------------

        private int MaxQuantity => _equipment != null ? 1 : Mathf.Max(1, inventory.GetCount(_item));

        private void Use()
        {
            if (_item != null && !inventory.TryUse(_item)) ShowMessage("Cannot use now", false);
        }

        private void ToggleEquip()
        {
            if (_equipment == null || equipment == null) return;
            if (equipment.IsEquipped(_equipment))
            {
                equipment.Unequip(_equipment);
                return;
            }
            var result = equipment.Equip(_equipment);
            if (result == EquipResult.LevelTooLow) ShowMessage($"Requires Lv.{_equipment.Data.RequiredLevel}", false);
        }

        private void Discard()
        {
            if (_item == null) return;
            if (_equipment != null)
            {
                if (equipment != null && equipment.IsEquipped(_equipment)) { ShowMessage("Unequip the item first", false); return; }
                inventory.RemoveEquipment(_equipment);
            }
            else inventory.RemoveItem(_item, Mathf.Clamp(_quantity, 1, MaxQuantity));
        }

        private void ToggleQuickSlot(int index)
        {
            if (_item == null || quickSlots == null || !_item.IsUsable) return;
            bool here = quickSlots.FindItemSlot(_item) == index;
            quickSlots.AssignItem(index, here ? null : _item);
            Refresh();
        }

        private void ShowMessage(string text, bool ok)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = ok ? okColor : errorColor;
        }

        // ---------------- View ----------------

        private void Refresh()
        {
            if (_item == null) return;
            var grade = _item.Grade;
            if (iconBackground != null) iconBackground.color = ItemGradeColors.Background(grade);
            if (icon != null) icon.color = _item.SlotColor;
            if (iconLabel != null) iconLabel.text = _item.ShortLabel;
            if (nameText != null)
            {
                nameText.text = _equipment != null ? _equipment.DisplayName : _item.DisplayName;
                nameText.color = ItemGradeColors.Text(grade);
            }

            bool isEquip = _item is EquipmentData;
            var data = _item as EquipmentData;
            bool equipped = _equipment != null && equipment != null && equipment.IsEquipped(_equipment);

            if (typeText != null)
                typeText.text = isEquip ? $"{grade}  ·  {data.SlotType}{(equipped ? "  ·  Equipped" : "")}" : $"{grade}  ·  {_item.Category}";
            if (levelText != null)
            {
                levelText.gameObject.SetActive(isEquip);
                if (isEquip)
                {
                    bool ok = equipment == null || equipment.MeetsLevel(data);
                    levelText.text = $"Required Lv.{data.RequiredLevel}";
                    levelText.color = ok ? new Color(0.85f, 0.85f, 0.85f) : errorColor;
                }
            }

            if (statsText != null)
                statsText.text = _equipment != null
                    ? StatText.Lines(_equipment.Stats, _equipment.EnhanceLevel > 0 ? data.Stats : (RPGAuto.Data.StatBlock?)null)
                    : string.Empty;
            if (descriptionText != null) descriptionText.text = _item.Description;

            int owned = _equipment != null ? 1 : inventory.GetCount(_item);
            string sell = _item.IsSellable ? $"{_item.SellPrice:N0} G" : "-";
            if (infoText != null)
                infoText.text = _equipment != null
                    ? $"Weight {_item.Weight:0.#}     Sell {sell}     Enhance +{_equipment.EnhanceLevel} / +{data.MaxEnhance}"
                    : $"Owned {owned:N0}     Weight {_item.Weight:0.#} (x{owned:N0} = {_item.Weight * owned:#,0.#})     Sell {sell}";

            if (useButton != null) useButton.gameObject.SetActive(_item.IsUsable);
            if (equipButton != null) equipButton.gameObject.SetActive(_equipment != null);
            if (equipButtonText != null) equipButtonText.text = equipped ? "UNEQUIP" : "EQUIP";
            if (enhanceButton != null) enhanceButton.gameObject.SetActive(_equipment != null);

            bool stack = _equipment == null;
            _quantity = Mathf.Clamp(_quantity, 1, MaxQuantity);
            if (quantityRow != null)
            {
                if (stack) quantityRow.Show("Discard Quantity", null, _quantity.ToString());
                else quantityRow.Hide();
            }

            bool usable = _item.IsUsable && quickSlots != null;
            if (quickSlotGroup != null) quickSlotGroup.SetActive(usable);
            if (usable)
            {
                int assigned = quickSlots.FindItemSlot(_item);
                for (int i = 0; i < quickSlotButtons.Length; i++)
                    if (quickSlotButtons[i] != null && quickSlotButtons[i].targetGraphic != null)
                        quickSlotButtons[i].targetGraphic.color = i == assigned ? quickSlotAssigned : quickSlotNormal;
            }
        }
    }
}
