using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>HP/MP 등 게이지 바 (Prefab: UI_StatBar)</summary>
    public class StatBarView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text valueText;
        [SerializeField] private bool showValue = true;

        public void SetValue(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (fill != null) fill.fillAmount = ratio;
            if (valueText != null)
                valueText.text = showValue ? $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}" : string.Empty;
        }

        public void SetColor(Color color)
        {
            if (fill != null) fill.color = color;
        }
    }
}
