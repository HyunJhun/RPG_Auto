using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>장비 강화 팝업: 성공률, 비용(골드 + 강화 주문서), 능력치 변화 미리보기, 결과 표시</summary>
    public class EnhancePopup : MonoBehaviour, IGameWindow
    {
        [Header("Data")]
        [SerializeField] private EnhanceTable table;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;

        [Header("Parts")]
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text rateText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text costText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button enhanceButton;

        [Header("Style")]
        [SerializeField] private Color okColor = new(0.55f, 0.95f, 0.55f);
        [SerializeField] private Color failColor = new(1f, 0.45f, 0.4f);
        [SerializeField] private Color lackColor = new(1f, 0.45f, 0.4f);

        private EquipmentInstance _target;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => SetOpen(false));
            enhanceButton?.onClick.AddListener(DoEnhance);
            SetOpen(false);
        }

        public void Open(EquipmentInstance equipment)
        {
            _target = equipment;
            if (resultText != null) resultText.text = string.Empty;
            SetOpen(true);
            Refresh();
        }

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
        }

        private void DoEnhance()
        {
            if (_target == null) return;
            var result = EnhanceService.TryEnhance(_target, table, wallet, inventory);
            if (resultText != null)
            {
                resultText.text = EnhanceService.ToMessage(result, _target);
                resultText.color = result == EnhanceResult.Success ? okColor : failColor;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (_target == null || table == null) return;
            var data = _target.Data;
            int lv = _target.EnhanceLevel;
            int max = EnhanceService.MaxLevelOf(_target, table);

            if (iconBackground != null) iconBackground.color = ItemGradeColors.Background(data.Grade);
            if (icon != null) icon.color = data.SlotColor;
            if (iconLabel != null) iconLabel.text = data.ShortLabel;

            bool hasStep = table.TryGetStep(lv, out var step);
            bool atMax = lv >= max || !hasStep;
            if (titleText != null)
            {
                titleText.text = atMax ? $"{_target.DisplayName}  (MAX)" : $"{_target.DisplayName}  →  +{lv + 1}";
                titleText.color = ItemGradeColors.Text(data.Grade);
            }

            if (atMax)
            {
                if (rateText != null) rateText.text = "최대 강화 단계입니다";
                if (statsText != null) statsText.text = StatText.Lines(_target.Stats);
                if (costText != null) costText.text = string.Empty;
                if (enhanceButton != null) enhanceButton.interactable = false;
                return;
            }

            if (rateText != null)
                rateText.text = $"성공 확률  {step.successRate * 100f:0}%\n실패 시  {(step.downgradeOnFail ? "<color=#FF7366>강화 단계 -1</color>" : "변화 없음")}";
            if (statsText != null) statsText.text = StatText.Lines(data.GetStats(lv + 1), _target.Stats);

            int scrolls = inventory.GetCount(table.ScrollItem);
            string goldColor = ColorUtility.ToHtmlStringRGB(wallet.CanAfford(step.goldCost) ? Color.white : lackColor);
            string scrollColor = ColorUtility.ToHtmlStringRGB(scrolls >= step.scrollCost ? Color.white : lackColor);
            string scrollName = table.ScrollItem != null ? table.ScrollItem.DisplayName : "주문서";
            if (costText != null)
                costText.text = $"<color=#{goldColor}>골드  {step.goldCost:N0}  (보유 {wallet.Gold:N0})</color>\n" +
                                $"<color=#{scrollColor}>{scrollName}  x{step.scrollCost}  (보유 {scrolls:N0})</color>";
            if (enhanceButton != null)
                enhanceButton.interactable = EnhanceService.Check(_target, table, wallet, inventory) == EnhanceResult.Success;
        }
    }
}
