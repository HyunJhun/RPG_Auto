using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Serialization;

namespace RPGAuto.UI
{
    /// <summary>
    /// UGUI 가상 조이스틱. Input System의 화면 컨트롤(OnScreenControl)로 동작해
    /// 드래그 방향을 가상 게임패드의 왼쪽 스틱으로 보낸다.
    /// → Input Action "Move"가 키보드·실제 게임패드·이 조이스틱을 똑같이 받는다.
    /// </summary>
    public class VirtualJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField, Range(0f, 1f)] private float deadZone = 0.1f;
        [Tooltip("배경 반지름 대비 핸들이 움직일 수 있는 비율")]
        [SerializeField, Range(0.1f, 1.5f)] private float handleRange = 1f;

        [InputControl(layout = "Vector2")]
        [FormerlySerializedAs("controlPath")]
        [SerializeField] private string m_ControlPath = "<Gamepad>/leftStick";

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        /// <summary>현재 조이스틱 방향 (크기 0~1)</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>방향 변경 알림 (Observer)</summary>
        public event Action<Vector2> OnDirectionChanged;

        private Canvas _canvas;

        private void Awake()
        {
            if (background == null) background = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
        }

        protected override void OnDisable()
        {
            ResetHandle();
            base.OnDisable();
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, uiCamera, out Vector2 local))
                return;

            Vector2 radius = background.rect.size * 0.5f;
            Vector2 center = background.rect.center;
            Vector2 raw = new((local.x - center.x) / radius.x, (local.y - center.y) / radius.y);
            if (raw.sqrMagnitude > 1f) raw.Normalize();

            if (handle != null) handle.anchoredPosition = raw * radius * handleRange;

            SetDirection(raw.magnitude < deadZone ? Vector2.zero : raw);
        }

        public void OnPointerUp(PointerEventData eventData) => ResetHandle();

        private void ResetHandle()
        {
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            SetDirection(Vector2.zero);
        }

        private void SetDirection(Vector2 value)
        {
            if (value == Direction) return;
            Direction = value;
            if (control != null) SendValueToControl(value);
            OnDirectionChanged?.Invoke(value);
        }
    }
}
