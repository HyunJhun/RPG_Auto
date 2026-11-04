using RPGAuto.InputHandling;
using RPGAuto.QuickSlots;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 퀵슬롯 칸 (Prefab: UI_QuickSlot)
    /// - 탭: 사용
    /// - 누른 채 위로 슬라이드: 자동 사용 ON/OFF
    /// - 자동 사용 중: 테두리 맥동 + 회전하는 반짝임 + AUTO 배지
    /// </summary>
    public class QuickSlotView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Binding")]
        [SerializeField] private QuickSlotBar bar;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private int slotIndex;

        [Header("Parts")]
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text countText;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text cooldownText;
        [SerializeField] private Image autoGlow;
        [SerializeField] private RectTransform autoShine;
        [SerializeField] private GameObject autoBadge;
        [SerializeField] private Color emptyColor = new(0.12f, 0.12f, 0.14f, 0.8f);

        [Header("Gesture")]
        [Tooltip("위로 이만큼(기준 해상도 px) 끌면 자동 사용 토글")]
        [SerializeField] private float swipeUpThreshold = 45f;

        [Header("Auto FX")]
        [SerializeField] private float glowPulseSpeed = 4f;
        [SerializeField] private float shineSpinSpeed = 240f;

        private QuickSlot _slot;
        private Canvas _canvas;
        private Vector2 _pressPosition;
        private bool _pressing;
        private bool _swipeConsumed;
        private float _punch;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            _slot = bar != null ? bar.GetSlot(slotIndex) : null;
            if (_slot == null) return;

            _slot.OnActionChanged += HandleActionChanged;
            _slot.OnAutoUseChanged += HandleAutoChanged;
            HandleActionChanged(_slot);
            HandleAutoChanged(_slot);
        }

        private void OnDestroy()
        {
            if (_slot == null) return;
            _slot.OnActionChanged -= HandleActionChanged;
            _slot.OnAutoUseChanged -= HandleAutoChanged;
        }

        // ---------------- Gesture ----------------

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressing = true;
            _swipeConsumed = false;
            _pressPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressing || _swipeConsumed || _slot == null || _slot.IsEmpty) return;

            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            if (eventData.position.y - _pressPosition.y < swipeUpThreshold * scale) return;

            _swipeConsumed = true;
            _slot.ToggleAutoUse();
            _punch = 1f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressing) return;
            _pressing = false;
            if (_swipeConsumed) return;

            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            if ((eventData.position - _pressPosition).magnitude <= swipeUpThreshold * scale)
                input?.PressQuickSlot(slotIndex);
        }

        // ---------------- View ----------------

        private void HandleActionChanged(QuickSlot slot)
        {
            var action = slot.Action;
            if (icon != null) icon.color = action != null ? action.Color : emptyColor;
            if (label != null) label.text = action != null ? action.Label : string.Empty;
        }

        private void HandleAutoChanged(QuickSlot slot)
        {
            bool on = slot.IsAutoUse;
            if (autoGlow != null) autoGlow.gameObject.SetActive(on);
            if (autoShine != null) autoShine.gameObject.SetActive(on);
            if (autoBadge != null) autoBadge.SetActive(on);
        }

        private void Update()
        {
            if (_slot == null) return;
            var action = _slot.Action;

            float remain = action != null ? action.CooldownRemaining : 0f;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = action != null ? action.CooldownNormalized : 0f;
            if (cooldownText != null) cooldownText.text = remain > 0f ? remain.ToString(remain < 1f ? "0.0" : "0") : string.Empty;
            if (countText != null) countText.text = action?.CountText ?? string.Empty;

            if (_slot.IsAutoUse)
            {
                float t = Time.unscaledTime;
                if (autoGlow != null)
                {
                    var c = autoGlow.color;
                    c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * glowPulseSpeed));
                    autoGlow.color = c;
                }
                if (autoShine != null) autoShine.Rotate(0f, 0f, -shineSpinSpeed * Time.unscaledDeltaTime);
            }

            // 토글 순간 살짝 튀는 피드백
            if (_punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime * 5f);
                transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(_punch * Mathf.PI));
            }
        }
    }
}
