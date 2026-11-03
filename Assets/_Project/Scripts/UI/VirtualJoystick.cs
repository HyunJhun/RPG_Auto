using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGAuto.UI
{
    /// <summary>UGUI 기반 가상 조이스틱 (모바일 직접 이동용, PC에서도 마우스로 조작 가능)</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField, Range(0f, 1f)] private float deadZone = 0.1f;
        [Tooltip("배경 반지름 대비 핸들이 움직일 수 있는 비율")]
        [SerializeField, Range(0.1f, 1.5f)] private float handleRange = 1f;

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

        private void OnDisable() => ResetHandle();

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
            OnDirectionChanged?.Invoke(value);
        }
    }
}
