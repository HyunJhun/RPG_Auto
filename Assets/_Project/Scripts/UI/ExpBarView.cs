using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>화면 최하단 경험치 바 + "EXP 26.001%"</summary>
    public class ExpBarView : MonoBehaviour
    {
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private Image fill;
        [SerializeField] private Text percentText;

        private void OnEnable()
        {
            if (progression == null) return;
            progression.OnExpChanged += HandleExp;
            HandleExp(progression.Exp, progression.RequiredExp);
        }

        private void OnDisable()
        {
            if (progression != null) progression.OnExpChanged -= HandleExp;
        }

        private void HandleExp(long exp, long required)
        {
            float ratio = progression.ExpRatio;
            if (fill != null) fill.fillAmount = ratio;
            if (percentText != null)
                percentText.text = progression.IsMaxLevel ? "경험치 MAX" : $"경험치 {ratio * 100f:0.000}%";
        }
    }
}
