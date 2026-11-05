using RPGAuto.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>뽑기 결과 한 칸 (Prefab: UI_GachaResultSlot). Reveal로 등장 연출.</summary>
    public class GachaResultSlotView : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text countText;
        [SerializeField] private Text gradeText;
        [SerializeField] private float popDuration = 0.25f;

        private float _revealAt;
        private bool _revealed;

        public void Bind(in GachaResult result, float revealDelay)
        {
            var color = GachaService.GradeColor(result.Grade);
            if (frame != null) frame.color = color;
            if (icon != null) icon.color = result.Item.SlotColor;
            if (label != null) label.text = result.Item.ShortLabel;
            if (countText != null) countText.text = result.Count > 1 ? $"x{result.Count}" : string.Empty;
            if (gradeText != null)
            {
                gradeText.text = result.Grade.ToString().ToUpperInvariant();
                gradeText.color = color;
            }

            _revealAt = Time.unscaledTime + revealDelay;
            _revealed = false;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_revealed) return;
            float t = (Time.unscaledTime - _revealAt) / popDuration;
            if (t < 0f) return;
            if (t >= 1f)
            {
                _revealed = true;
                transform.localScale = Vector3.one;
                return;
            }
            // 살짝 커졌다가 원래 크기로 (back-out)
            float s = 1f + 0.25f * Mathf.Sin(t * Mathf.PI) ;
            transform.localScale = Vector3.one * Mathf.Min(s, t * 4f);
        }
    }
}
