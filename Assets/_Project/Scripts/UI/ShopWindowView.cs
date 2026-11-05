using System.Collections.Generic;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 상점 창 (단축키 O / 메뉴 버튼). 구매/판매 탭, 수량 선택, 테스트용 골드 지급.
    /// 거래 규칙은 ShopService, 화면 갱신은 지갑·인벤토리 이벤트 구독(Observer).
    /// </summary>
    public class ShopWindowView : MonoBehaviour, IGameWindow
    {
        private enum Tab { Buy, Sell }

        [Header("Data")]
        [SerializeField] private ShopData shop;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private ShopRowView rowPrefab;
        [SerializeField] private GameObject listEmptyHint;
        [SerializeField] private Text goldText;
        [SerializeField] private Text messageText;

        [Header("Detail")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private GameObject detailEmptyHint;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text detailIconLabel;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailDescription;
        [SerializeField] private Text detailInfo;
        [SerializeField] private OptionRowView quantityRow;
        [SerializeField] private Button maxButton;
        [SerializeField] private Text totalText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionText;

        [Header("Test")]
        [SerializeField] private Button testGoldButton;
        [SerializeField] private long testGoldAmount = 10000;
        [SerializeField] private Button sellAllMaterialsButton;

        [Header("Style")]
        [SerializeField] private Color tabOn = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color tabOff = new(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color okColor = new(0.55f, 0.95f, 0.55f);
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f);

        private readonly List<ShopRowView> _rows = new();
        private readonly List<ItemData> _sellItems = new();
        private Tab _tab = Tab.Buy;
        private int _selected = -1;
        private int _quantity = 1;
        private bool _dirty;
        private float _messageTimer;

        private void Awake()
        {
            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            buyTabButton?.onClick.AddListener(() => SelectTab(Tab.Buy));
            sellTabButton?.onClick.AddListener(() => SelectTab(Tab.Sell));
            maxButton?.onClick.AddListener(() => SetQuantity(MaxQuantity()));
            actionButton?.onClick.AddListener(DoTransaction);
            testGoldButton?.onClick.AddListener(() =>
            {
                wallet.AddGold(testGoldAmount);
                ShowMessage($"[TEST] Gold +{testGoldAmount:N0}", true);
            });
            sellAllMaterialsButton?.onClick.AddListener(SellAllMaterials);
            if (quantityRow != null)
            {
                quantityRow.OnMinus += () => SetQuantity(_quantity - 1);
                quantityRow.OnPlus += () => SetQuantity(_quantity + 1);
            }
            if (titleText != null && shop != null) titleText.text = shop.ShopName.ToUpperInvariant();
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnShopTogglePressed += Toggle;
            if (inventory != null) inventory.OnItemCountChanged += HandleItemChanged;
            if (wallet != null) wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDisable()
        {
            if (input != null) input.OnShopTogglePressed -= Toggle;
            if (inventory != null) inventory.OnItemCountChanged -= HandleItemChanged;
            if (wallet != null) wallet.OnGoldChanged -= HandleGoldChanged;
        }

        private void HandleItemChanged(ItemData item, int count) => _dirty = true;
        private void HandleGoldChanged(long gold, long delta) => _dirty = true;

        private void LateUpdate()
        {
            if (!IsOpen) return;
            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.unscaledDeltaTime;
                if (_messageTimer <= 0f && messageText != null) messageText.text = string.Empty;
            }
            if (!_dirty) return;
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
            if (messageText != null) messageText.text = string.Empty;
            SelectTab(_tab);
        }

        // ---------------- Actions ----------------

        private void SelectTab(Tab tab)
        {
            _tab = tab;
            _selected = -1;
            _quantity = 1;
            Refresh();
            if (RowCount > 0) Select(0);
        }

        private void Select(int index)
        {
            _selected = index;
            _quantity = 1;
            Refresh();
        }

        private void SetQuantity(int value)
        {
            _quantity = Mathf.Clamp(value, 1, MaxQuantity());
            Refresh();
        }

        private int MaxQuantity()
        {
            if (_selected < 0) return 1;
            return _tab == Tab.Buy
                ? ShopService.MaxAffordable(shop.Products[_selected].price, wallet)
                : Mathf.Max(1, inventory.GetCount(_sellItems[_selected]));
        }

        private void DoTransaction()
        {
            if (_selected < 0) return;
            ShopResult result;
            string done;
            if (_tab == Tab.Buy)
            {
                var p = shop.Products[_selected];
                result = ShopService.Buy(p, _quantity, wallet, inventory);
                done = $"Bought {p.item.DisplayName} x{_quantity}  (-{p.price * _quantity:N0} G)";
            }
            else
            {
                var item = _sellItems[_selected];
                result = ShopService.Sell(item, _quantity, wallet, inventory);
                done = $"Sold {item.DisplayName} x{_quantity}  (+{item.SellPrice * _quantity:N0} G)";
            }

            bool ok = result == ShopResult.Ok;
            ShowMessage(ok ? done : ShopService.ToMessage(result), ok);
            if (!ok) return;

            _quantity = 1;
            // 판매로 칸이 사라졌으면 선택을 앞쪽으로 옮긴다
            RebuildSellList();
            if (_selected >= RowCount) _selected = RowCount - 1;
            Refresh();
        }

        private void SellAllMaterials()
        {
            long total = 0;
            int kinds = 0;
            var items = new List<ItemData>(inventory.Items);
            foreach (var item in items)
            {
                if (item.Category != ItemCategory.Material || !item.IsSellable) continue;
                int count = inventory.GetCount(item);
                if (ShopService.Sell(item, count, wallet, inventory) != ShopResult.Ok) continue;
                total += item.SellPrice * count;
                kinds++;
            }
            ShowMessage(kinds > 0 ? $"Sold {kinds} kinds of materials  (+{total:N0} G)" : "No materials to sell", kinds > 0);
            if (_tab == Tab.Sell) SelectTab(Tab.Sell);
        }

        private void ShowMessage(string text, bool ok)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = ok ? okColor : errorColor;
            _messageTimer = 2.5f;
        }

        // ---------------- View ----------------

        private int RowCount => _tab == Tab.Buy ? (shop != null ? shop.Products.Count : 0) : _sellItems.Count;

        private void RebuildSellList()
        {
            _sellItems.Clear();
            foreach (var item in inventory.Items)
                if (item.IsSellable) _sellItems.Add(item);
        }

        private void Refresh()
        {
            if (_tab == Tab.Sell) RebuildSellList();
            int count = RowCount;
            if (_selected >= count) _selected = count - 1;

            // 탭 버튼
            if (buyTabButton != null) buyTabButton.targetGraphic.color = _tab == Tab.Buy ? tabOn : tabOff;
            if (sellTabButton != null) sellTabButton.targetGraphic.color = _tab == Tab.Sell ? tabOn : tabOff;
            if (sellAllMaterialsButton != null) sellAllMaterialsButton.gameObject.SetActive(_tab == Tab.Sell);

            // 목록 (필요한 만큼만 생성하고 재사용)
            while (_rows.Count < count)
            {
                var row = Instantiate(rowPrefab, listContent);
                row.name = $"UI_ShopRow_{_rows.Count + 1:00}";
                row.Index = _rows.Count;
                row.OnClicked += r => Select(r.Index);
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < count;
                _rows[i].gameObject.SetActive(active);
                if (!active) continue;
                if (_tab == Tab.Buy)
                {
                    var p = shop.Products[i];
                    _rows[i].Bind(p.item, $"Owned {inventory.GetCount(p.item):N0}", p.price, i == _selected);
                }
                else
                {
                    var item = _sellItems[i];
                    _rows[i].Bind(item, $"Owned {inventory.GetCount(item):N0}", item.SellPrice, i == _selected);
                }
            }
            if (listEmptyHint != null) listEmptyHint.SetActive(count == 0);
            if (goldText != null) goldText.text = $"Gold  {wallet.Gold:N0}";

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            bool has = _selected >= 0;
            if (detailRoot != null) detailRoot.SetActive(has);
            if (detailEmptyHint != null) detailEmptyHint.SetActive(!has);
            if (!has) return;

            ItemData item;
            long unit;
            if (_tab == Tab.Buy)
            {
                var p = shop.Products[_selected];
                item = p.item;
                unit = p.price;
            }
            else
            {
                item = _sellItems[_selected];
                unit = item.SellPrice;
            }

            _quantity = Mathf.Clamp(_quantity, 1, MaxQuantity());
            long total = unit * _quantity;

            if (detailIcon != null) detailIcon.color = item.SlotColor;
            if (detailIconLabel != null) detailIconLabel.text = item.ShortLabel;
            if (detailName != null) detailName.text = item.DisplayName;
            if (detailDescription != null) detailDescription.text = item.Description;
            if (detailInfo != null)
                detailInfo.text = $"{(_tab == Tab.Buy ? "Price" : "Sell Price")}  {unit:N0} G\nOwned  {inventory.GetCount(item):N0}";
            quantityRow?.Show("Quantity", null, _quantity.ToString());

            bool affordable = _tab == Tab.Sell || wallet.CanAfford(total);
            if (totalText != null)
            {
                totalText.text = $"Total  {total:N0} G";
                totalText.color = affordable ? Color.white : errorColor;
            }
            if (actionText != null) actionText.text = _tab == Tab.Buy ? "BUY" : "SELL";
        }
    }
}
