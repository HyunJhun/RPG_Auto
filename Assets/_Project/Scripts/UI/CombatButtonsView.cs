using RPGAuto.InputHandling;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>공격 버튼 / 자동 전투 토글 버튼 (입력은 PlayerInputHandler 경유)</summary>
    public class CombatButtonsView : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerCombat combat;

        [Header("Attack")]
        [SerializeField] private Button attackButton;

        [Header("Auto Combat")]
        [SerializeField] private Button autoButton;
        [SerializeField] private Image autoBackground;
        [SerializeField] private Text autoLabel;
        [SerializeField] private Color autoOnColor = new(0.95f, 0.7f, 0.15f);
        [SerializeField] private Color autoOffColor = new(0.25f, 0.25f, 0.28f);

        private void Awake()
        {
            attackButton?.onClick.AddListener(() => input?.PressAttack());
            autoButton?.onClick.AddListener(() => input?.PressAutoCombatToggle());
        }

        private void OnEnable()
        {
            if (combat == null) return;
            combat.OnAutoCombatChanged += RefreshAuto;
            RefreshAuto(combat.IsAutoEnabled);
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnAutoCombatChanged -= RefreshAuto;
        }

        private void RefreshAuto(bool enabled)
        {
            if (autoBackground != null) autoBackground.color = enabled ? autoOnColor : autoOffColor;
            if (autoLabel != null) autoLabel.text = enabled ? "AUTO\nON" : "AUTO\nOFF";
        }
    }
}
