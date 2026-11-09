using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Core.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 피격 시 대상 머리 위로 떠오르는 데미지 숫자.
    /// CombatStats의 전역 피격/빗나감 이벤트를 구독(Observer)하고, 숫자는 Object Pool로 재사용한다.
    /// 플레이어가 주거나 받은 피해만 표시한다.
    /// </summary>
    public class DamageTextManager : MonoBehaviour
    {
        [SerializeField] private CombatStats player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text textPrefab;

        [Header("Motion")]
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseDistance = 70f;
        [SerializeField] private float horizontalJitter = 26f;
        [SerializeField] private float popScale = 1.35f;

        [Header("Style")]
        [SerializeField] private Color dealtColor = new(1f, 0.92f, 0.55f);
        [SerializeField] private Color takenColor = new(1f, 0.3f, 0.3f);
        [SerializeField] private Color missColor = new(0.8f, 0.8f, 0.85f);
        [SerializeField] private int dealtFontSize = 32;
        [SerializeField] private int takenFontSize = 26;
        [SerializeField] private int missFontSize = 24;

        private class Entry
        {
            public Text Text;
            public Vector3 World;
            public Vector2 Offset;
            public Color Color;
            public float Age;
        }

        private RectTransform _root;
        private PrefabPool<Text> _pool;
        private readonly List<Entry> _active = new();

        private void Awake()
        {
            _root = (RectTransform)transform;
            if (viewCamera == null) viewCamera = Camera.main;
            _pool = new PrefabPool<Text>(textPrefab, _root, defaultCapacity: 24, maxSize: 96);
        }

        private void OnEnable()
        {
            CombatStats.OnAnyDamaged += HandleDamaged;
            CombatStats.OnAnyMissed += HandleMissed;
        }

        private void OnDisable()
        {
            CombatStats.OnAnyDamaged -= HandleDamaged;
            CombatStats.OnAnyMissed -= HandleMissed;
        }

        private void HandleDamaged(CombatStats target, float amount, CombatStats attacker)
        {
            if (target == player)
                Spawn(target, Mathf.CeilToInt(amount).ToString("N0"), takenColor, takenFontSize);
            else if (attacker == player)
                Spawn(target, Mathf.CeilToInt(amount).ToString("N0"), dealtColor, dealtFontSize);
        }

        private void HandleMissed(CombatStats target, CombatStats attacker)
        {
            if (target == player || attacker == player)
                Spawn(target, "MISS", missColor, missFontSize);
        }

        private void Spawn(CombatStats target, string value, Color color, int fontSize)
        {
            var text = _pool.Get();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.rectTransform.SetAsLastSibling();

            _active.Add(new Entry
            {
                Text = text,
                World = HeadOf(target),
                Offset = new Vector2(Random.Range(-horizontalJitter, horizontalJitter), 0f),
                Color = color,
                Age = 0f,
            });
        }

        private static Vector3 HeadOf(CombatStats target)
        {
            // 렌더러 상단 = 머리 위 (프리미티브 교체 후에도 동작)
            var r = target.GetComponentInChildren<Renderer>();
            if (r != null) return new Vector3(target.transform.position.x, r.bounds.max.y + 0.2f, target.transform.position.z);
            return target.transform.position + Vector3.up * 2f;
        }

        private void LateUpdate()
        {
            if (viewCamera == null) return;
            float dt = Time.deltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                e.Age += dt;
                float t = e.Age / lifetime;
                if (t >= 1f)
                {
                    _pool.Release(e.Text);
                    _active.RemoveAt(i);
                    continue;
                }

                Vector3 screen = viewCamera.WorldToScreenPoint(e.World);
                bool visible = screen.z > 0f;
                e.Text.enabled = visible;
                if (!visible) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
                float rise = 1f - (1f - t) * (1f - t); // ease-out
                e.Text.rectTransform.anchoredPosition = local + e.Offset + Vector2.up * (riseDistance * rise);

                // 처음 0.12초 튀어나왔다가 원래 크기로
                float pop = t < 0.13f ? Mathf.Lerp(popScale, 1f, t / 0.13f) : 1f;
                e.Text.rectTransform.localScale = Vector3.one * pop;

                // 마지막 35% 동안 페이드 아웃
                var c = e.Color;
                c.a = t < 0.65f ? 1f : Mathf.InverseLerp(1f, 0.65f, t);
                e.Text.color = c;
            }
        }
    }
}
