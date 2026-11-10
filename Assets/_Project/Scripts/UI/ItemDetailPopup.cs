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
        [SerializeField] private DisassembleTable disassembleTable;

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
        [SerializeField] private Button lockButton;
        [SerializeField] private Text lockButtonText;
        [SerializeField] private Button disassembleButton;
        [SerializeField] private Text disassembleButtonText;
        [Tooltip("분해 버튼을 한 번 누른 뒤 이 시간 안에 다시 눌러야 분해된다")]
        [SerializeField] private float confirmWindow = 3f;
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
        private float _confirmUntil = -1f;

        public bool IsOpen => window != null && window.gameObject.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => SetOpen(false));
            useButton?.onClick.AddListener(Use);
            equipButton?.onClick.AddListener(ToggleEquip);
            enhanceButton?.onClick.AddListener(() => { if (_equipment != null) enhancePopup?.Open(_equipment); });
            discardButton?.onClick.AddListener(Discard);
            lockButton?.onClick.AddListener(ToggleLock);
            disassembleButton?.onClick.AddListener(Disassemble);
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
            _confirmUntil = -1f;
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
            if (_item != null && !inventory.TryUse(_item)) ShowMessage("지금은 사용할 수 없습니다", false);
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
            if (result == EquipResult.LevelTooLow) ShowMessage($"Lv.{_equipment.Data.RequiredLevel} 이상 착용 가능", false);
        }

        /// <summary>지금 보고 있는 아이템의 인벤토리 칸 (장비 = 개체, 묶음 = 아이템)</summary>
        private InventoryEntry CurrentEntry => _equipment != null ? inventory.FindEntry(_equipment) : inventory.FindStack(_item);

        private void ToggleLock()
        {
            var entry = CurrentEntry;
            if (entry == null) return;
            inventory.SetLocked(entry, !entry.IsLocked);
            ShowMessage(entry.IsLocked ? "잠금: 판매·버리기·분해·자동 담기에서 제외됩니다" : "잠금을 해제했습니다", true);
        }

        /// <summary>분해: 되돌릴 수 없으므로 두 번 눌러야 실행</summary>
        private void Disassemble()
        {
            var entry = CurrentEntry;
            var check = DisassembleService.Check(entry, inventory, equipment);
            if (check != DisassembleResult.Ok) { ShowMessage(DisassembleService.ToMessage(check), false); return; }

            int reward = disassembleTable != null ? disassembleTable.RewardCount(entry.Equipment) : 0;
            string rewardName = disassembleTable != null && disassembleTable.RewardItem != null ? disassembleTable.RewardItem.DisplayName : "재료";
            if (Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = Time.unscaledTime + confirmWindow;
                if (disassembleButtonText != null) disassembleButtonText.text = "분해 확인";
                ShowMessage($"한 번 더 누르면 분해합니다 ({rewardName} {reward}개 획득)", false);
                return;
            }

            _confirmUntil = -1f;
            var result = DisassembleService.Disassemble(entry, inventory, equipment, disassembleTable, out int got);
            if (result != DisassembleResult.Ok) ShowMessage(DisassembleService.ToMessage(result), false);
            // 성공하면 장비가 사라지므로 팝업은 HandleChanged에서 닫힌다
        }

        private void Update()
        {
            if (_confirmUntil > 0f && Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = -1f;
                if (disassembleButtonText != null) disassembleButtonText.text = "분해";
            }
        }

        private void Discard()
        {
            if (_item == null) return;
            if (CurrentEntry is { IsLocked: true }) { ShowMessage("잠긴 아이템은 버릴 수 없습니다", false); return; }
            if (_equipment != null)
            {
                if (equipment != null && equipment.IsEquipped(_equipment)) { ShowMessage("먼저 장착을 해제하세요", false); return; }
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

        /// <summary>
        /// 장착 중: 강화로 오른 양(기본 능력치 대비) 표시.
        /// 미장착: 같은 부위 장착 장비(반지는 약한 쪽/빈 칸)와 비교해 증감 표시.
        /// </summary>
        private string BuildStatsText(EquipmentData data, bool equipped)
        {
            if (_equipment == null || data == null) return string.Empty;
            if (equipped || equipment == null)
                return StatText.Lines(_equipment.Stats, _equipment.EnhanceLevel > 0 ? data.Stats : (RPGAuto.Data.StatBlock?)null);

            var target = equipment.GetComparisonTarget(data);
            float diff = EquipmentPower.Score(_equipment) - EquipmentPower.Score(target);
            string verdict = Mathf.Approximately(diff, 0f) ? "<color=#CCCCCC>전투력 동일</color>"
                : diff > 0 ? $"<color=#7CFC7C>전투력 +{diff:0.#}</color>" : $"<color=#FF7366>전투력 {diff:0.#}</color>";
            string header = target != null
                ? $"<size=15><color=#A0A8B8>장착 중인 {target.DisplayName}와 비교</color>  {verdict}</size>\n"
                : $"<size=15><color=#A0A8B8>빈 칸과 비교</color>  {verdict}</size>\n";
            return header + StatText.Lines(_equipment.Stats, target != null ? target.Stats : new RPGAuto.Data.StatBlock());
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
                typeText.text = isEquip ? $"{GameText.Grade(grade)}  ·  {GameText.Slot(data.SlotType)}{(equipped ? "  ·  장착 중" : "")}" : $"{GameText.Grade(grade)}  ·  {GameText.Category(_item.Category)}";
            if (levelText != null)
            {
                levelText.gameObject.SetActive(isEquip);
                if (isEquip)
                {
                    bool ok = equipment == null || equipment.MeetsLevel(data);
                    levelText.text = $"착용 레벨 Lv.{data.RequiredLevel}";
                    levelText.color = ok ? new Color(0.85f, 0.85f, 0.85f) : errorColor;
                }
            }

            if (statsText != null) statsText.text = BuildStatsText(data, equipped);
            if (descriptionText != null) descriptionText.text = _item.Description;

            int owned = _equipment != null ? 1 : inventory.GetCount(_item);
            string sell = _item.IsSellable ? $"{_item.SellPrice:N0} G" : "-";
            if (infoText != null)
                infoText.text = _equipment != null
                    ? $"무게 {_item.Weight:0.#}     판매가 {sell}     강화 +{_equipment.EnhanceLevel} / +{data.MaxEnhance}"
                    : $"보유 {owned:N0}     무게 {_item.Weight:0.#} (x{owned:N0} = {_item.Weight * owned:#,0.#})     판매가 {sell}";

            if (useButton != null) useButton.gameObject.SetActive(_item.IsUsable);
            if (equipButton != null) equipButton.gameObject.SetActive(_equipment != null);
            if (equipButtonText != null) equipButtonText.text = equipped ? "해제" : "장착";
            if (enhanceButton != null) enhanceButton.gameObject.SetActive(_equipment != null);

            var current = CurrentEntry;
            bool locked = current != null && current.IsLocked;
            if (lockButton != null) lockButton.gameObject.SetActive(current != null);
            if (lockButtonText != null) lockButtonText.text = locked ? "잠금 해제" : "잠금";
            if (disassembleButton != null) disassembleButton.gameObject.SetActive(_equipment != null && current != null);
            if (disassembleButtonText != null && _confirmUntil < 0f) disassembleButtonText.text = "분해";
            if (discardButton != null) discardButton.interactable = !locked;

            bool stack = _equipment == null;
            _quantity = Mathf.Clamp(_quantity, 1, MaxQuantity);
            if (quantityRow != null)
            {
                if (stack) quantityRow.Show("버릴 수량", null, _quantity.ToString());
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
