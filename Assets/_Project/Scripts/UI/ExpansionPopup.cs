using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>인벤토리 칸/무게 확장 팝업. 늘릴 양을 슬라이더로 정하고 비용을 확인한 뒤 확장한다.</summary>
    public class ExpansionPopup : MonoBehaviour, IGameWindow
    {
        [Header("Data")]
        [SerializeField] private InventoryExpansionConfig config;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;

        [Header("Slots")]
        [SerializeField] private Slider slotSlider;
        [SerializeField] private Text slotText;
        [SerializeField] private Text slotCostText;

        [Header("Weight")]
        [SerializeField] private Slider weightSlider;
        [SerializeField] private Text weightText;
        [SerializeField] private Text weightCostText;

        [Header("Footer")]
        [SerializeField] private Text totalText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button confirmButton;

        [Header("Style")]
        [SerializeField] private Color okColor = new(0.55f, 0.95f, 0.55f);
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f);

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => SetOpen(false));
            confirmButton?.onClick.AddListener(Confirm);
            slotSlider?.onValueChanged.AddListener(_ => Refresh());
            weightSlider?.onValueChanged.AddListener(_ => Refresh());
            SetOpen(false);
        }

        public void Open()
        {
            if (resultText != null) resultText.text = string.Empty;
            SetOpen(true);
            ResetSliders();
        }

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
        }

        private int AddSlots => slotSlider != null ? Mathf.RoundToInt(slotSlider.value) : 0;
        private int AddWeightSteps => weightSlider != null ? Mathf.RoundToInt(weightSlider.value) : 0;

        private void ResetSliders()
        {
            if (slotSlider != null)
            {
                slotSlider.wholeNumbers = true;
                slotSlider.minValue = 0;
                slotSlider.maxValue = config.MaxAddSlots(inventory);
                slotSlider.SetValueWithoutNotify(0);
                slotSlider.interactable = slotSlider.maxValue > 0;
            }
            if (weightSlider != null)
            {
                weightSlider.wholeNumbers = true;
                weightSlider.minValue = 0;
                weightSlider.maxValue = config.MaxAddWeightSteps(inventory);
                weightSlider.SetValueWithoutNotify(0);
                weightSlider.interactable = weightSlider.maxValue > 0;
            }
            Refresh();
        }

        private void Confirm()
        {
            var result = InventoryExpansionService.Expand(config, inventory, wallet, AddSlots, AddWeightSteps);
            if (resultText != null)
            {
                resultText.text = result switch
                {
                    ExpansionResult.Ok => "인벤토리를 확장했습니다!",
                    ExpansionResult.Nothing => "확장할 양을 선택하세요",
                    ExpansionResult.NotEnoughGold => "골드가 부족합니다",
                    _ => "최대치를 넘을 수 없습니다"
                };
                resultText.color = result == ExpansionResult.Ok ? okColor : errorColor;
            }
            if (result == ExpansionResult.Ok) ResetSliders();
        }

        private void Refresh()
        {
            int slots = AddSlots;
            int steps = AddWeightSteps;
            float addWeight = steps * config.WeightStep;

            if (slotText != null)
                slotText.text = config.MaxAddSlots(inventory) == 0
                    ? $"칸 수  {inventory.Capacity}  (최대)"
                    : $"칸 수  {inventory.Capacity}  →  {inventory.Capacity + slots}   (+{slots})";
            if (slotCostText != null) slotCostText.text = $"{slots * config.GoldPerSlot:N0} G";

            if (weightText != null)
                weightText.text = config.MaxAddWeightSteps(inventory) == 0
                    ? $"최대 무게  {inventory.MaxWeight:#,0}  (최대)"
                    : $"최대 무게  {inventory.MaxWeight:#,0}  →  {inventory.MaxWeight + addWeight:#,0}   (+{addWeight:#,0})";
            if (weightCostText != null) weightCostText.text = $"{steps * config.GoldPerWeightStep:N0} G";

            long total = config.Cost(slots, steps);
            if (totalText != null)
            {
                totalText.text = $"합계  {total:N0} G     (보유 {wallet.Gold:N0} G)";
                totalText.color = wallet.CanAfford(total) ? Color.white : errorColor;
            }
            if (confirmButton != null) confirmButton.interactable = total > 0 && wallet.CanAfford(total);
        }
    }
}
