using System.Collections.Generic;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.QuickSlots;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>상점이 열려 있을 때 인벤토리 클릭/버튼을 상점으로 넘기는 연결점</summary>
    public interface IInventoryShopHook
    {
        void OnInventoryEntryClicked(InventoryEntry entry);
        void AutoFill();
        void ClearBasket();
        void OnInventoryClosed();
    }

    /// <summary>
    /// 인벤토리 창 (단축키 I / 메뉴 버튼) — 프라시아 전기 인벤토리 형식.
    /// 우측 세로 패널, 좌측 분류 필터, 4열 칸(등급색/Lv/+강화/E/수량), 하단 무게·칸 수와 확장 버튼.
    /// 칸을 누르면 상세 팝업, 상점이 열려 있으면 판매 바구니에 담는다.
    /// </summary>
    public class InventoryWindowView : MonoBehaviour, IGameWindow
    {
        public enum Filter { All, Equipment, Accessory, Consumable, Material }

        private static readonly string[] QuickKeys = { "7", "8", "9", "0" };

        [Header("Data")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private QuickSlotBar quickSlots;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private RectTransform gridFrame;
        [SerializeField] private InventorySlotView slotPrefab;
        [SerializeField] private Button[] filterButtons = new Button[5];

        [Header("Footer")]
        [SerializeField] private Text weightText;
        [SerializeField] private Text slotText;
        [SerializeField] private Button expandButton;

        [Header("Shop Mode")]
        [SerializeField] private GameObject shopBar;
        [SerializeField] private Button autoFillButton;
        [SerializeField] private Button clearBasketButton;

        [Header("Popups")]
        [SerializeField] private ItemDetailPopup detailPopup;
        [SerializeField] private ExpansionPopup expansionPopup;

        [Header("Style")]
        [SerializeField] private Color filterOn = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color filterOff = new(0.16f, 0.16f, 0.2f, 1f);
        [SerializeField] private Color weightNormal = new(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color weightOver = new(1f, 0.35f, 0.3f);
        [SerializeField] private float gridBottomNormal = 64f;
        [SerializeField] private float gridBottomShop = 124f;

        private readonly List<InventorySlotView> _slots = new();
        private readonly List<InventoryEntry> _filtered = new();
        private Filter _filter = Filter.All;
        private InventoryEntry _selected;
        private IInventoryShopHook _shopHook;
        private bool _dirty;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            expandButton?.onClick.AddListener(() => expansionPopup?.Open());
            autoFillButton?.onClick.AddListener(() => _shopHook?.AutoFill());
            clearBasketButton?.onClick.AddListener(() => _shopHook?.ClearBasket());
            for (int i = 0; i < filterButtons.Length; i++)
            {
                var f = (Filter)i; // 클로저 캡처용
                filterButtons[i]?.onClick.AddListener(() => SetFilter(f));
            }
            SetShopHook(null);
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnInventoryTogglePressed += Toggle;
            if (inventory != null)
            {
                inventory.OnInventoryChanged += MarkDirty;
                inventory.OnCapacityChanged += HandleCapacity;
                inventory.OnWeightChanged += HandleWeight;
            }
            if (equipment != null) equipment.OnEquipmentChanged += MarkDirty;
        }

        private void OnDisable()
        {
            if (input != null) input.OnInventoryTogglePressed -= Toggle;
            if (inventory != null)
            {
                inventory.OnInventoryChanged -= MarkDirty;
                inventory.OnCapacityChanged -= HandleCapacity;
                inventory.OnWeightChanged -= HandleWeight;
            }
            if (equipment != null) equipment.OnEquipmentChanged -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;
        private void HandleCapacity(int _) => _dirty = true;
        private void HandleWeight(float current, float max) => RefreshFooter();

        private void LateUpdate()
        {
            // 한 프레임에 여러 번 바뀌어도 한 번만 갱신
            if (!_dirty || !IsOpen) return;
            _dirty = false;
            Refresh();
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (open)
            {
                _dirty = false;
                Refresh();
                return;
            }

            if (detailPopup != null && detailPopup.IsOpen) detailPopup.SetOpen(false);
            if (expansionPopup != null && expansionPopup.IsOpen) expansionPopup.SetOpen(false);
            // 상점과 함께 열려 있었다면 상점도 닫는다
            var hook = _shopHook;
            if (hook != null)
            {
                SetShopHook(null);
                hook.OnInventoryClosed();
            }
        }

        /// <summary>상점이 열리면 판매 바구니 연결 (null = 일반 모드)</summary>
        public void SetShopHook(IInventoryShopHook hook)
        {
            _shopHook = hook;
            if (shopBar != null) shopBar.SetActive(hook != null);
            if (gridFrame != null)
                gridFrame.offsetMin = new Vector2(gridFrame.offsetMin.x, hook != null ? gridBottomShop : gridBottomNormal);
        }

        public void SetFilter(Filter filter)
        {
            _filter = filter;
            Refresh();
        }

        // ---------------- View ----------------

        private void HandleSlotClicked(InventorySlotView slot)
        {
            if (slot.Entry == null) return;
            _selected = slot.Entry;

            if (_shopHook != null) _shopHook.OnInventoryEntryClicked(slot.Entry);
            else detailPopup?.Show(slot.Entry, ItemDetailPopup.Dock.LeftOfRightPanel);
            Refresh();
        }

        private void Refresh()
        {
            _filtered.Clear();
            foreach (var e in inventory.Entries)
                if (Matches(e)) _filtered.Add(e);
            if (_selected != null && !inventory.HasEntry(_selected)) _selected = null;

            // 전체 보기는 칸 수만큼(빈 칸 포함), 필터 보기는 해당 아이템만
            int count = _filter == Filter.All ? Mathf.Max(inventory.Capacity, _filtered.Count) : _filtered.Count;
            while (_slots.Count < count)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.name = $"UI_InventorySlot_{_slots.Count + 1:000}";
                slot.OnClicked += HandleSlotClicked;
                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool active = i < count;
                slot.gameObject.SetActive(active);
                if (!active) continue;

                var entry = i < _filtered.Count ? _filtered[i] : null;
                if (entry == null) { slot.Clear(null); continue; }

                bool equipped = entry.IsEquipment && equipment != null && equipment.IsEquipped(entry.Equipment);
                bool levelOk = entry.Item is not EquipmentData data || equipment == null || equipment.MeetsLevel(data);
                slot.Bind(entry, entry == _selected, equipped, QuickKeyOf(entry.Item), levelOk);
            }

            for (int i = 0; i < filterButtons.Length; i++)
                if (filterButtons[i] != null && filterButtons[i].targetGraphic != null)
                    filterButtons[i].targetGraphic.color = i == (int)_filter ? filterOn : filterOff;

            RefreshFooter();
        }

        private void RefreshFooter()
        {
            if (inventory == null) return;
            if (weightText != null)
            {
                weightText.text = $"WEIGHT  {inventory.CurrentWeight:#,0.#} / {inventory.MaxWeight:#,0}";
                weightText.color = inventory.IsOverweight ? weightOver : weightNormal;
            }
            if (slotText != null) slotText.text = $"{inventory.UsedSlots} / {inventory.Capacity}";
        }

        private bool Matches(InventoryEntry e) => _filter switch
        {
            Filter.Equipment => e.Item is EquipmentData d && !d.IsAccessory,
            Filter.Accessory => e.Item is EquipmentData d && d.IsAccessory,
            Filter.Consumable => e.Item.Category == ItemCategory.Consumable,
            Filter.Material => e.Item.Category == ItemCategory.Material,
            _ => true
        };

        private string QuickKeyOf(ItemData item)
        {
            if (item == null || quickSlots == null || !item.IsUsable) return null;
            int slot = quickSlots.FindItemSlot(item);
            return slot >= 0 ? QuickKeys[slot] : null;
        }
    }
}
