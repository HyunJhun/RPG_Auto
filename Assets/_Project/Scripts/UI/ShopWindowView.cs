using System.Collections.Generic;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 상점 창 (단축키 O / 메뉴 버튼) — 프라시아 전기 상인 형식.
    /// 좌측: 구매/판매 탭, 세율, 일괄 구매, 수량 슬라이더 목록, 예상 무게·지불 금액, 완료.
    /// 우측: 인벤토리를 함께 열고, 인벤토리 칸을 누르거나 [자동 담기]로 판매 바구니에 담는다.
    /// </summary>
    public class ShopWindowView : MonoBehaviour, IGameWindow, IInventoryShopHook
    {
        private enum Tab { Buy, Sell }

        [Header("Data")]
        [SerializeField] private ShopData shop;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private InventoryWindowView inventoryWindow;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;
        [SerializeField] private Text taxText;
        [SerializeField] private Button bulkBuyButton;
        [SerializeField] private Button clearButton;

        [Header("List")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private ShopTradeRowView rowPrefab;
        [SerializeField] private Text listEmptyHint;

        [Header("Summary")]
        [SerializeField] private Text weightText;
        [SerializeField] private Text paymentText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button doneButton;
        [SerializeField] private Text doneText;

        [Header("Test")]
        [SerializeField] private Button testGoldButton;
        [SerializeField] private long testGoldAmount = 10000;

        [Header("Style")]
        [SerializeField] private Color tabOn = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color tabOff = new(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color okColor = new(0.55f, 0.95f, 0.55f);
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f);

        private readonly List<ShopTradeRowView> _rows = new();
        private readonly List<BuyLine> _buy = new();
        private readonly List<SellLine> _sell = new();
        private Tab _tab = Tab.Buy;
        private float _messageTimer;
        private bool _dirty;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            if (shop != null)
                foreach (var p in shop.Products) _buy.Add(new BuyLine { Product = p, Quantity = 0 });

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            buyTabButton?.onClick.AddListener(() => SelectTab(Tab.Buy));
            sellTabButton?.onClick.AddListener(() => SelectTab(Tab.Sell));
            bulkBuyButton?.onClick.AddListener(BulkBuy);
            clearButton?.onClick.AddListener(ClearBasket);
            doneButton?.onClick.AddListener(Done);
            testGoldButton?.onClick.AddListener(() =>
            {
                wallet.AddGold(testGoldAmount);
                ShowMessage($"[TEST] Gold +{testGoldAmount:N0}", true);
            });
            if (titleText != null && shop != null) titleText.text = shop.ShopName;
            if (taxText != null && shop != null) taxText.text = $"Current Tax Rate  {shop.TaxRate * 100f:0}%";
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnShopTogglePressed += Toggle;
            if (inventory != null) inventory.OnInventoryChanged += MarkDirty;
            if (wallet != null) wallet.OnGoldChanged += HandleGold;
        }

        private void OnDisable()
        {
            if (input != null) input.OnShopTogglePressed -= Toggle;
            if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
            if (wallet != null) wallet.OnGoldChanged -= HandleGold;
        }

        private void MarkDirty() => _dirty = true;
        private void HandleGold(long gold, long delta) => _dirty = true;

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
            Rebuild();
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);

            if (open)
            {
                if (messageText != null) messageText.text = string.Empty;
                // 상인 창을 열면 인벤토리를 옆에 함께 연다 (판매 바구니 연결)
                if (inventoryWindow != null)
                {
                    inventoryWindow.SetShopHook(this);
                    if (!inventoryWindow.IsOpen) inventoryWindow.SetOpen(true);
                }
                SelectTab(_tab);
                return;
            }

            ResetQuantities();
            _sell.Clear();
            if (inventoryWindow != null)
            {
                inventoryWindow.SetShopHook(null); // 먼저 끊어서 OnInventoryClosed가 다시 불리지 않게
                if (inventoryWindow.IsOpen) inventoryWindow.SetOpen(false);
            }
        }

        // ---------------- IInventoryShopHook ----------------

        public void OnInventoryEntryClicked(InventoryEntry entry)
        {
            if (entry == null) return;
            if (!entry.Item.IsSellable) { ShowMessage(ShopService.ToMessage(ShopResult.NotSellable), false); return; }
            if (entry.IsEquipment && equipment != null && equipment.IsEquipped(entry.Equipment))
            {
                ShowMessage(ShopService.ToMessage(ShopResult.Equipped), false);
                return;
            }

            int index = _sell.FindIndex(l => l.Entry == entry);
            if (index < 0) _sell.Add(new SellLine { Entry = entry, Quantity = entry.Count });
            if (_tab != Tab.Sell) SelectTab(Tab.Sell);
            else Rebuild();
        }

        /// <summary>자동 담기: [자동 담기] 대상으로 지정된 전리품(ItemData.AutoSell)을 바구니에 담는다</summary>
        public void AutoFill()
        {
            int added = 0;
            foreach (var e in inventory.Entries)
            {
                if (!e.Item.AutoSell) continue;
                if (_sell.Exists(l => l.Entry == e)) continue;
                _sell.Add(new SellLine { Entry = e, Quantity = e.Count });
                added++;
            }
            ShowMessage(added > 0 ? $"Added {added} kinds of loot" : "No loot to add", added > 0);
            SelectTab(Tab.Sell);
        }

        public void ClearBasket()
        {
            if (_tab == Tab.Buy) ResetQuantities();
            else _sell.Clear();
            Rebuild();
        }

        public void OnInventoryClosed() => SetOpen(false);

        // ---------------- Actions ----------------

        private void SelectTab(Tab tab)
        {
            _tab = tab;
            Rebuild();
        }

        private void ResetQuantities()
        {
            for (int i = 0; i < _buy.Count; i++) _buy[i] = new BuyLine { Product = _buy[i].Product, Quantity = 0 };
        }

        /// <summary>일괄 구매: 상품마다 정해 둔 수량(restockTarget)까지 채우도록 수량을 설정</summary>
        private void BulkBuy()
        {
            for (int i = 0; i < _buy.Count; i++)
            {
                var p = _buy[i].Product;
                int need = p.restockTarget > 0 ? Mathf.Max(0, p.restockTarget - inventory.GetCount(p.item)) : 0;
                _buy[i] = new BuyLine { Product = p, Quantity = Mathf.Min(need, ShopService.MaxQuantity) };
            }
            Rebuild();
        }

        private void Done()
        {
            ShopResult result;
            string done;
            if (_tab == Tab.Buy)
            {
                long cost = ShopService.TotalCost(_buy, shop.TaxRate);
                result = ShopService.Buy(_buy, shop.TaxRate, wallet, inventory);
                done = $"Purchase complete  (-{cost:N0} G)";
                if (result == ShopResult.Ok) ResetQuantities();
            }
            else
            {
                long total = ShopService.TotalSell(_sell);
                result = ShopService.Sell(_sell, wallet, inventory, equipment);
                done = $"Sale complete  (+{total:N0} G)";
                if (result == ShopResult.Ok) _sell.Clear();
            }
            bool ok = result == ShopResult.Ok;
            ShowMessage(ok ? done : ShopService.ToMessage(result), ok);
            Rebuild();
        }

        private void ShowMessage(string text, bool ok)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = ok ? okColor : errorColor;
            _messageTimer = 3f;
        }

        // ---------------- View ----------------

        private void Rebuild()
        {
            // 인벤토리에서 사라진 칸은 판매 바구니에서도 뺀다
            _sell.RemoveAll(l => !inventory.HasEntry(l.Entry));
            for (int i = 0; i < _sell.Count; i++)
            {
                var l = _sell[i];
                if (l.Quantity > l.Entry.Count) _sell[i] = new SellLine { Entry = l.Entry, Quantity = l.Entry.Count };
            }

            bool buy = _tab == Tab.Buy;
            if (buyTabButton != null) buyTabButton.targetGraphic.color = buy ? tabOn : tabOff;
            if (sellTabButton != null) sellTabButton.targetGraphic.color = buy ? tabOff : tabOn;
            if (bulkBuyButton != null) bulkBuyButton.gameObject.SetActive(buy);
            if (doneText != null) doneText.text = buy ? "BUY" : "SELL";

            int count = buy ? _buy.Count : _sell.Count;
            while (_rows.Count < count)
            {
                var row = Instantiate(rowPrefab, listContent);
                row.name = $"UI_ShopTradeRow_{_rows.Count + 1:00}";
                row.Index = _rows.Count;
                row.OnQuantityChanged += HandleQuantity;
                row.OnRemove += HandleRemove;
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < count;
                _rows[i].gameObject.SetActive(active);
                if (!active) continue;

                if (buy)
                {
                    var l = _buy[i];
                    long unit = ShopService.UnitBuyPrice(l.Product, shop.TaxRate);
                    _rows[i].Bind(l.Product.item, l.Product.item.DisplayName, $"Owned {inventory.GetCount(l.Product.item):N0}",
                        unit, Mathf.Max(l.Quantity, MaxBuyable(l.Product, unit)), l.Quantity, false);
                }
                else
                {
                    var l = _sell[i];
                    var e = l.Entry;
                    string name = e.IsEquipment ? e.Equipment.DisplayName : e.Item.DisplayName;
                    _rows[i].Bind(e.Item, name, $"Owned {e.Count:N0}", e.Item.SellPrice, e.Count, l.Quantity, true);
                }
            }

            if (listEmptyHint != null)
            {
                listEmptyHint.gameObject.SetActive(count == 0);
                listEmptyHint.text = buy ? "No products" : "Tap items in your inventory\nor use AUTO FILL";
            }
            RefreshSummary();
        }

        /// <summary>슬라이더 최대치: 999개, 지금 골드로 살 수 있는 수, 남은 무게로 들 수 있는 수 중 최소</summary>
        private int MaxBuyable(in ShopData.Product product, long unitPrice)
        {
            int max = ShopService.MaxAffordable(unitPrice, wallet);
            float w = product.item.Weight;
            if (w > 0f) max = Mathf.Min(max, Mathf.Max(0, Mathf.FloorToInt((inventory.MaxWeight - inventory.CurrentWeight) / w)));
            return Mathf.Clamp(max, 0, ShopService.MaxQuantity);
        }

        private void HandleQuantity(ShopTradeRowView row, int quantity)
        {
            int i = row.Index;
            if (_tab == Tab.Buy && i < _buy.Count) _buy[i] = new BuyLine { Product = _buy[i].Product, Quantity = quantity };
            else if (_tab == Tab.Sell && i < _sell.Count) _sell[i] = new SellLine { Entry = _sell[i].Entry, Quantity = quantity };
            RefreshSummary(); // 슬라이더를 다시 만들지 않도록 요약만 갱신
        }

        private void HandleRemove(ShopTradeRowView row)
        {
            if (_tab == Tab.Sell && row.Index < _sell.Count) _sell.RemoveAt(row.Index);
            Rebuild();
        }

        private void RefreshSummary()
        {
            bool buy = _tab == Tab.Buy;
            float delta = buy ? ShopService.TotalWeight(_buy) : -ShopService.TotalWeight(_sell);
            float expected = inventory.CurrentWeight + delta;
            if (weightText != null)
            {
                weightText.text = $"Expected Weight  {expected:#,0.#} / {inventory.MaxWeight:#,0}";
                weightText.color = expected > inventory.MaxWeight ? errorColor : Color.white;
            }

            if (buy)
            {
                long cost = ShopService.TotalCost(_buy, shop.TaxRate);
                if (paymentText != null)
                {
                    paymentText.text = $"Payment  {cost:N0} G";
                    paymentText.color = wallet.CanAfford(cost) ? new Color(1f, 0.85f, 0.35f) : errorColor;
                }
            }
            else if (paymentText != null)
            {
                paymentText.text = $"Receive  {ShopService.TotalSell(_sell):N0} G";
                paymentText.color = new Color(1f, 0.85f, 0.35f);
            }
            if (goldText != null) goldText.text = $"Gold  {wallet.Gold:N0}";
        }
    }
}
