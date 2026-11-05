using System;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 설정 한 줄 (Prefab: UI_OptionRow) — 라벨 + ON/OFF 토글 + [-] 값 [+]
    /// 토글/값 표시 여부는 Show()의 인자로 정한다.
    /// </summary>
    public class OptionRowView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Text valueText;

        [Header("Style")]
        [SerializeField] private Color onColor = new(0.2f, 0.55f, 0.3f, 1f);
        [SerializeField] private Color offColor = new(0.25f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color disabledTextColor = new(0.5f, 0.5f, 0.55f, 1f);

        private Color _labelColor;

        public event Action OnToggle;
        public event Action OnMinus;
        public event Action OnPlus;

        private void Awake()
        {
            _labelColor = label != null ? label.color : Color.white;
            toggleButton?.onClick.AddListener(() => OnToggle?.Invoke());
            minusButton?.onClick.AddListener(() => OnMinus?.Invoke());
            plusButton?.onClick.AddListener(() => OnPlus?.Invoke());
        }

        /// <param name="toggle">null이면 토글 숨김</param>
        /// <param name="value">null이면 [-] 값 [+] 숨김</param>
        /// <param name="interactable">false면 회색 표시 + 입력 막음</param>
        public void Show(string text, bool? toggle, string value, bool interactable = true)
        {
            gameObject.SetActive(true);
            if (label != null)
            {
                label.text = text;
                label.color = interactable ? _labelColor : disabledTextColor;
            }

            if (toggleButton != null)
            {
                toggleButton.gameObject.SetActive(toggle.HasValue);
                toggleButton.interactable = interactable;
                if (toggle.HasValue)
                {
                    if (toggleButton.targetGraphic != null) toggleButton.targetGraphic.color = toggle.Value ? onColor : offColor;
                    if (toggleText != null) toggleText.text = toggle.Value ? "ON" : "OFF";
                }
            }

            bool stepper = value != null;
            if (valueText != null)
            {
                valueText.gameObject.SetActive(stepper);
                valueText.text = value ?? string.Empty;
            }
            // 토글이 꺼져 있으면 값은 보여주되 조절은 막는다
            bool stepEnabled = interactable && (!toggle.HasValue || toggle.Value);
            if (minusButton != null) { minusButton.gameObject.SetActive(stepper); minusButton.interactable = stepEnabled; }
            if (plusButton != null) { plusButton.gameObject.SetActive(stepper); plusButton.interactable = stepEnabled; }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
