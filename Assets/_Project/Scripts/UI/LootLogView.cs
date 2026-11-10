using System.Collections.Generic;
using RPGAuto.Core.Pooling;
using RPGAuto.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 전리품 획득 로그. PlayerLooter 이벤트를 구독(Observer)하고 줄(Text)은 Object Pool로 재사용한다.
    /// 최신 항목이 맨 위에 쌓이고 일정 시간 뒤 사라진다.
    /// </summary>
    public class LootLogView : MonoBehaviour
    {
        [SerializeField] private PlayerLooter looter;
        [SerializeField] private RectTransform lineContainer;
        [SerializeField] private Text linePrefab;
        [SerializeField] private int maxLines = 6;
        [SerializeField] private float lineLifetime = 3.5f;
        [SerializeField] private float fadeDuration = 0.6f;

        [Header("Style")]
        [SerializeField] private Color goldColor = new(1f, 0.85f, 0.35f);
        [SerializeField] private Color itemColor = new(0.92f, 0.95f, 1f);
        [SerializeField] private Color warningColor = new(1f, 0.4f, 0.35f);

        private class Line
        {
            public Text Text;
            public Color Color;
            public float Age;
        }

        private PrefabPool<Text> _pool;
        private readonly List<Line> _lines = new(); // 0 = 가장 오래된 줄

        private void Awake()
        {
            _pool = new PrefabPool<Text>(linePrefab, lineContainer, defaultCapacity: maxLines, maxSize: maxLines * 2);
        }

        private void OnEnable()
        {
            if (looter == null) return;
            looter.OnLootAcquired += HandleLoot;
            looter.OnAcquireBlocked += HandleBlocked;
        }

        private void OnDisable()
        {
            if (looter == null) return;
            looter.OnLootAcquired -= HandleLoot;
            looter.OnAcquireBlocked -= HandleBlocked;
        }

        private void HandleLoot(LootEntry entry) =>
            Push(entry.IsGold ? $"골드 +{entry.Gold:N0}" : $"{entry.Item.DisplayName}  x{entry.Count}",
                 entry.IsGold ? goldColor : itemColor);

        private void HandleBlocked(RPGAuto.Items.AcceptResult reason) =>
            Push(reason == RPGAuto.Items.AcceptResult.Overweight ? "무게가 초과되어 더 들 수 없습니다" : "인벤토리가 가득 찼습니다", warningColor);

        private void Push(string message, Color color)
        {
            if (_lines.Count >= maxLines)
            {
                _pool.Release(_lines[0].Text);
                _lines.RemoveAt(0);
            }

            var text = _pool.Get();
            text.text = message;
            text.color = color;
            text.transform.SetAsFirstSibling(); // 최신 줄을 맨 위로 (VerticalLayoutGroup)
            _lines.Add(new Line { Text = text, Color = color, Age = 0f });
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                var line = _lines[i];
                line.Age += dt;
                if (line.Age >= lineLifetime)
                {
                    _pool.Release(line.Text);
                    _lines.RemoveAt(i);
                    continue;
                }

                var c = line.Color;
                c.a = Mathf.Clamp01((lineLifetime - line.Age) / fadeDuration);
                line.Text.color = c;
            }
        }
    }
}
