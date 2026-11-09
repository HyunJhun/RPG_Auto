using RPGAuto.Player.Assist;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>HUD 보조 모드 토글 버튼 + 왕복 중 상태 문구</summary>
    public class AssistButtonView : MonoBehaviour
    {
        [SerializeField] private AssistController assist;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private GameObject statusRoot;
        [SerializeField] private Text statusText;
        [SerializeField] private Color onColor = new(0.3f, 0.6f, 0.95f);
        [SerializeField] private Color offColor = new(0.25f, 0.25f, 0.28f);

        private bool _lastMode;

        private void Awake() => button?.onClick.AddListener(() => assist.ToggleAssistMode());

        private void OnEnable()
        {
            if (assist == null) return;
            assist.OnAssistModeChanged += RefreshMode;
            assist.OnStatusChanged += RefreshStatus;
            RefreshMode(assist.IsAssistEnabled);
            RefreshStatus(assist.Status);
        }

        private void OnDisable()
        {
            if (assist == null) return;
            assist.OnAssistModeChanged -= RefreshMode;
            assist.OnStatusChanged -= RefreshStatus;
        }

        // 설정 창에서 바꾼 값도 반영 (설정 창은 AutoCombatSettings 값을 직접 바꾼다)
        private void Update()
        {
            if (assist != null && assist.IsAssistEnabled != _lastMode) RefreshMode(assist.IsAssistEnabled);
        }

        private void RefreshMode(bool enabled)
        {
            _lastMode = enabled;
            if (background != null) background.color = enabled ? onColor : offColor;
            if (label != null) label.text = enabled ? "ASSIST\nON" : "ASSIST\nOFF";
        }

        private void RefreshStatus(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (statusRoot != null) statusRoot.SetActive(show);
            if (statusText != null) statusText.text = text;
        }
    }
}
