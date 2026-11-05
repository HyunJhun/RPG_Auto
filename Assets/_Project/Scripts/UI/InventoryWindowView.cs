using System.Collections.Generic;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.QuickSlots;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 인벤토리 창 (단축키 I / 메뉴 버튼).
    /// 칸은 UI_InventorySlot 프리팹으로 생성하고, 인벤토리·지갑 변경 이벤트를 구독(Observer)해 갱신한다.
    /// 선택한 아이템은 사용하거나 물약 퀵슬롯(7~0)에 등록할 수 있다.
    /// </summary>
    public class InventoryWindowView : MonoBehaviour, IGameWindow
    {
        private static readonly string[] QuickKeys = { "7", "8", "9", "0" };

        [Header("Data")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private QuickSlotBar quickSlots;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private InventorySlotView slotPrefab;
        [SerializeField] private Text goldText;
        [SerializeField] private Text capacityText;

        [Header("Detail")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private GameObject detailEmptyHint;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text detailIconLabel;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailCategory;
        [SerializeField] private Text detailCount;
        [SerializeField] private Text detailDescription;
        [SerializeField] private Button useButton;
        [SerializeField] private GameObject quickSlotGroup;
        [SerializeField] private Button[] quickSlotButtons = new Button[QuickSlotBar.ItemSlotCount];

        [Header("Style")]
        [SerializeField] private Color quickSlotNormal = new(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color quickSlotAssigned = new(0.85f, 0.65f, 0.2f, 1f);

        private readonly List<InventorySlotView> _slots = new();
        private ItemData _selected;
        private bool _dirty;

        private void Awake()
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.name = $"UI_InventorySlot_{i + 1:00}";
                slot.OnClicked += HandleSlotClicked;
                _slots.Add(slot);
            }

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            useButton?.onClick.AddListener(UseSelected);
            for (int i = 0; i < quickSlotButtons.Length; i++)
            {
                int index = i; // 클로저 캡처용
                quickSlotButtons[i]?.onClick.AddListener(() => ToggleQuickSlot(index));
            }
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnInventoryTogglePressed += Toggle;
            if (inventory != null) inventory.OnItemCountChanged += HandleItemChanged;
            if (wallet != null) wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDisable()
        {
            if (input != null) input.OnInventoryTogglePressed -= Toggle;
            if (inventory != null) inventory.OnItemCountChanged -= HandleItemChanged;
            if (wallet != null) wallet.OnGoldChanged -= HandleGoldChanged;
        }

        private void HandleItemChanged(ItemData item, int count) => _dirty = true;
        private void HandleGoldChanged(long gold, long delta) => RefreshGold();

        private void LateUpdate()
        {
            // 한 프레임에 여러 아이템이 바뀌어도 한 번만 갱신
            if (!_dirty || !IsOpen) return;
            _dirty = false;
            Refresh();
        }

        public bool IsOpen => window != null && window.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (!open) return;
            _dirty = false;
            Refresh();
        }

        // ---------------- Actions ----------------

        private void HandleSlotClicked(InventorySlotView slot)
        {
            if (slot.Item == null) return;
            // 선택된 칸을 다시 누르면 사용 (모바일 더블탭 대용)
            if (slot.Item == _selected && _selected.IsUsable) UseSelected();
            else _selected = slot.Item;
            Refresh();
        }

        private void UseSelected()
        {
            if (_selected != null) inventory.TryUse(_selected);
        }

        private void ToggleQuickSlot(int index)
        {
            if (_selected == null || quickSlots == null) return;
            bool alreadyHere = quickSlots.FindItemSlot(_selected) == index;
            quickSlots.AssignItem(index, alreadyHere ? null : _selected);
            Refresh();
        }

        // ---------------- View ----------------

        private void Refresh()
        {
            var items = inventory.Items;
            if (_selected != null && inventory.GetCount(_selected) <= 0) _selected = null;

            for (int i = 0; i < _slots.Count; i++)
            {
                var item = i < items.Count ? items[i] : null;
                _slots[i].Bind(item, inventory.GetCount(item), item != null && item == _selected, QuickKeyOf(item));
            }

            if (capacityText != null) capacityText.text = $"{items.Count} / {inventory.Capacity}";
            RefreshGold();
            RefreshDetail();
        }

        private void RefreshGold()
        {
            if (goldText != null && wallet != null) goldText.text = $"Gold  {wallet.Gold:N0}";
        }

        private void RefreshDetail()
        {
            bool has = _selected != null;
            if (detailRoot != null) detailRoot.SetActive(has);
            if (detailEmptyHint != null) detailEmptyHint.SetActive(!has);
            if (!has) return;

            if (detailIcon != null) detailIcon.color = _selected.SlotColor;
            if (detailIconLabel != null) detailIconLabel.text = _selected.ShortLabel;
            if (detailName != null) detailName.text = _selected.DisplayName;
            if (detailCategory != null) detailCategory.text = _selected.Category.ToString();
            if (detailCount != null) detailCount.text = $"Owned  {inventory.GetCount(_selected):N0}";
            if (detailDescription != null) detailDescription.text = _selected.Description;

            bool usable = _selected.IsUsable;
            if (useButton != null) useButton.gameObject.SetActive(usable);
            if (quickSlotGroup != null) quickSlotGroup.SetActive(usable && quickSlots != null);
            if (!usable || quickSlots == null) return;

            int assigned = quickSlots.FindItemSlot(_selected);
            for (int i = 0; i < quickSlotButtons.Length; i++)
            {
                var button = quickSlotButtons[i];
                if (button == null) continue;
                if (button.targetGraphic != null) button.targetGraphic.color = i == assigned ? quickSlotAssigned : quickSlotNormal;
            }
        }

        private string QuickKeyOf(ItemData item)
        {
            if (item == null || quickSlots == null) return null;
            int slot = quickSlots.FindItemSlot(item);
            return slot >= 0 ? QuickKeys[slot] : null;
        }
    }
}
