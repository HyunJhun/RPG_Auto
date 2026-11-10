using System.Collections.Generic;
using RPGAuto.Items;
using RPGAuto.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 자동 거래 설정 창.
    /// 판매(자동 담기): 장비/장신구/소모품/기타별 켜기 + 등급 선택 (칩으로 개별 선택, -/+로 'N등급 이하' 한 번에 지정).
    /// 구매(일괄 구매): 상품별 목표 수량.
    /// 상점 [자동 담기]·[일괄 구매]와 보조 모드가 이 설정을 쓴다.
    /// </summary>
    public class AutoTradeSettingsView : MonoBehaviour, IGameWindow
    {
        private static readonly string[] CategoryLabels = { "장비", "장신구", "소모품", "기타 (재료·전리품)" };

        [Header("Data")]
        [SerializeField] private PlayerTradeSettings trade;
        [SerializeField] private ShopData shop;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;

        [Header("Sell")]
        [SerializeField] private OptionRowView[] categoryRows = new OptionRowView[4];
        [Tooltip("분류 4개 × 등급 5개 (분류 순서대로)")]
        [SerializeField] private Button[] gradeChips = new Button[20];
        [SerializeField] private OptionRowView keepEnhancedRow;
        [SerializeField] private OptionRowView keepShopProductsRow;

        [Header("Buy")]
        [SerializeField] private RectTransform buyList;
        [SerializeField] private OptionRowView buyRowPrefab;

        [Header("Style")]
        [SerializeField] private Color chipOff = new(0.16f, 0.16f, 0.2f, 1f);
        [SerializeField] private Color chipLabelOff = new(0.5f, 0.5f, 0.55f);

        private readonly List<(OptionRowView row, ShopData.Product product)> _buyRows = new();

        public bool IsOpen => window != null && window.activeSelf;
        private AutoTradeSettings S => trade.Settings;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => SetOpen(false));
            resetButton?.onClick.AddListener(() => { trade.ResetToDefault(); Refresh(); });

            for (int c = 0; c < categoryRows.Length; c++)
            {
                var category = (TradeCategory)c; // 클로저 캡처용
                var row = categoryRows[c];
                if (row == null) continue;
                row.OnToggle += () => Apply(() => S.Rule(category).enabled = !S.Rule(category).enabled);
                row.OnMinus += () => Apply(() => StepUpTo(category, -1));
                row.OnPlus += () => Apply(() => StepUpTo(category, 1));
            }

            for (int i = 0; i < gradeChips.Length; i++)
            {
                var category = (TradeCategory)(i / AutoTradeSettings.GradeCount);
                var grade = (ItemGrade)(i % AutoTradeSettings.GradeCount);
                gradeChips[i]?.onClick.AddListener(() => Apply(() => S.Rule(category).Toggle(grade)));
            }

            if (keepEnhancedRow != null) keepEnhancedRow.OnToggle += () => Apply(() => S.keepEnhanced = !S.keepEnhanced);
            if (keepShopProductsRow != null) keepShopProductsRow.OnToggle += () => Apply(() => S.keepShopProducts = !S.keepShopProducts);

            BuildBuyRows();
            SetOpen(false);
        }

        private void BuildBuyRows()
        {
            if (shop == null || buyRowPrefab == null || buyList == null) return;
            foreach (var product in shop.Products)
            {
                if (product.item == null) continue;
                var p = product; // 클로저 캡처용
                var row = Instantiate(buyRowPrefab, buyList);
                row.name = "UI_AutoTradePopup_BuyRow_" + p.item.ItemId;
                int step = p.item.Category == ItemCategory.Consumable ? 10 : 5;
                row.OnMinus += () => Apply(() => S.SetBuyTarget(p.item, S.GetBuyTarget(p) - step));
                row.OnPlus += () => Apply(() => S.SetBuyTarget(p.item, Mathf.Min(999, S.GetBuyTarget(p) + step)));
                _buyRows.Add((row, p));
            }
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (open) Refresh();
        }

        /// <summary>값 변경 → 저장(Observer 알림) → 화면 갱신</summary>
        private void Apply(System.Action change)
        {
            change();
            trade.NotifyChanged();
            Refresh();
        }

        /// <summary>-/+ : 'N등급 이하'로 한 번에 지정 (현재 선택 중 가장 높은 등급 기준으로 한 단계 이동)</summary>
        private void StepUpTo(TradeCategory category, int dir)
        {
            var rule = S.Rule(category);
            int current = AutoTradeSettings.UpToGrade(rule.gradeMask) is { } g ? (int)g : HighestSelected(rule.gradeMask);
            int next = Mathf.Clamp(current + dir, 0, AutoTradeSettings.GradeCount - 1);
            rule.gradeMask = AutoTradeSettings.MaskUpTo((ItemGrade)next);
        }

        private static int HighestSelected(int mask)
        {
            for (int g = AutoTradeSettings.GradeCount - 1; g >= 0; g--)
                if ((mask & (1 << g)) != 0) return g;
            return 0;
        }

        private void Refresh()
        {
            if (trade == null) return;
            for (int c = 0; c < categoryRows.Length; c++)
            {
                var rule = S.Rule((TradeCategory)c);
                categoryRows[c]?.Show(CategoryLabels[c], rule.enabled, MaskLabel(rule.gradeMask)); // 꺼져 있으면 OptionRowView가 -/+만 막는다

                for (int g = 0; g < AutoTradeSettings.GradeCount; g++)
                {
                    var chip = gradeChips[c * AutoTradeSettings.GradeCount + g];
                    if (chip == null) continue;
                    bool on = rule.Includes((ItemGrade)g);
                    chip.interactable = rule.enabled;
                    // 선택: 등급 배경색에 등급 글자색을 섞어 밝게 (일반 등급도 꺼진 칸과 구분되게)
                    if (chip.targetGraphic != null)
                        chip.targetGraphic.color = on ? Color.Lerp(ItemGradeColors.Background((ItemGrade)g), ItemGradeColors.Text((ItemGrade)g), 0.35f) : chipOff;
                    var label = chip.GetComponentInChildren<Text>();
                    if (label != null) label.color = on ? Color.white : chipLabelOff;
                }
            }

            keepEnhancedRow?.Show("강화한 장비 제외", S.keepEnhanced, null);
            keepShopProductsRow?.Show("상점 판매 상품 제외 (물약·주문서)", S.keepShopProducts, null);

            foreach (var (row, product) in _buyRows)
            {
                int target = S.GetBuyTarget(product);
                row.Show(product.item.DisplayName, null, target > 0 ? $"{target}개" : "안 함");
            }
        }

        private static string MaskLabel(int mask)
        {
            if (mask == 0) return "선택 없음";
            if (AutoTradeSettings.UpToGrade(mask) is { } g) return $"{GameText.Grade(g)} 이하";
            int count = 0;
            for (int i = 0; i < AutoTradeSettings.GradeCount; i++) if ((mask & (1 << i)) != 0) count++;
            return $"{count}개 등급";
        }
    }
}
