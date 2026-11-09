using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Core.Pooling;
using RPGAuto.Monster;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 몬스터 머리 위 이름표 "Lv.N 이름". 이름표는 Object Pool로 재사용하고,
    /// 플레이어 레벨이 바뀌면(Observer) 레벨 차이 색상을 다시 계산한다.
    /// </summary>
    public class NameplateManager : MonoBehaviour
    {
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text nameplatePrefab;
        [SerializeField] private float maxVisibleDistance = 30f;

        private RectTransform _root;
        private PrefabPool<Text> _pool;
        private readonly Dictionary<MonsterController, Text> _plates = new();
        private readonly List<MonsterController> _toRemove = new();
        private int _colorLevel = -1;

        private void Awake()
        {
            _root = (RectTransform)transform;
            if (viewCamera == null) viewCamera = Camera.main;
            _pool = new PrefabPool<Text>(nameplatePrefab, _root, defaultCapacity: 16, maxSize: 64);
        }

        private void OnEnable()
        {
            if (progression != null) progression.OnLevelChanged += HandlePlayerLevel;
        }

        private void OnDisable()
        {
            if (progression != null) progression.OnLevelChanged -= HandlePlayerLevel;
        }

        private void HandlePlayerLevel(int level)
        {
            _colorLevel = level;
            foreach (var pair in _plates) ApplyText(pair.Key, pair.Value);
        }

        private void LateUpdate()
        {
            if (viewCamera == null) return;
            int playerLevel = progression != null ? progression.Level : 1;
            if (playerLevel != _colorLevel) HandlePlayerLevel(playerLevel);

            // 사라진 몬스터의 이름표 반환
            _toRemove.Clear();
            foreach (var pair in _plates)
                if (pair.Key == null || !pair.Key.IsAlive) _toRemove.Add(pair.Key);
            foreach (var m in _toRemove)
            {
                _pool.Release(_plates[m]);
                _plates.Remove(m);
            }

            // 새 몬스터에 이름표 배정 + 위치 갱신
            Vector3 camPos = viewCamera.transform.position;
            float sqrMax = maxVisibleDistance * maxVisibleDistance;
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.IsAlive) continue;

                if (!_plates.TryGetValue(m, out var plate))
                {
                    plate = _pool.Get();
                    _plates[m] = plate;
                    ApplyText(m, plate);
                }

                Vector3 world = m.HeadPosition;
                Vector3 screen = viewCamera.WorldToScreenPoint(world);
                bool visible = screen.z > 0f && (world - camPos).sqrMagnitude <= sqrMax;
                if (plate.enabled != visible) plate.enabled = visible;
                if (!visible) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
                plate.rectTransform.anchoredPosition = local;
            }
        }

        private void ApplyText(MonsterController m, Text plate)
        {
            string elite = m.Grade != MonsterGrade.Normal ? $"[{m.Grade}] " : string.Empty;
            plate.text = $"{elite}Lv.{m.Level} {m.DisplayName}";
            plate.color = LevelColorRule.Get(m.Level, _colorLevel < 1 ? 1 : _colorLevel);
        }
    }
}
