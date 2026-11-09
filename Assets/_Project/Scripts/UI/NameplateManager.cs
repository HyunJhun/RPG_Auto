using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Core.Pooling;
using RPGAuto.Items;
using RPGAuto.Loot;
using RPGAuto.Monster;
using RPGAuto.Npc;
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
        private readonly Dictionary<NpcInteractable, Text> _npcPlates = new();
        private readonly List<NpcInteractable> _npcToRemove = new();
        [SerializeField] private Color npcColor = new(0.55f, 1f, 0.6f);
        private readonly Dictionary<GroundLoot, Text> _lootPlates = new();
        private readonly List<GroundLoot> _lootToRemove = new();
        private readonly Dictionary<GroundLoot, int> _lootSpawnIds = new();
        [Tooltip("바닥 아이템 이름표 높이 (아이템 위치 기준)")]
        [SerializeField] private float lootLabelHeight = 0.6f;
        [SerializeField] private Color goldLabelColor = new(1f, 0.85f, 0.25f);
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

            UpdateNpcPlates(camPos, sqrMax);
            UpdateLootPlates(camPos, sqrMax);
        }

        /// <summary>바닥 전리품 이름표: 아이템 이름(등급 색) 또는 골드 (몬스터와 같은 풀 사용)</summary>
        private void UpdateLootPlates(Vector3 camPos, float sqrMax)
        {
            _lootToRemove.Clear();
            foreach (var pair in _lootPlates)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled) _lootToRemove.Add(pair.Key);
            foreach (var l in _lootToRemove)
            {
                _pool.Release(_lootPlates[l]);
                _lootPlates.Remove(l);
                _lootSpawnIds.Remove(l);
            }

            foreach (var loot in GroundLoot.Active)
            {
                if (!_lootPlates.TryGetValue(loot, out var plate))
                {
                    plate = _pool.Get();
                    _lootPlates[loot] = plate;
                    _lootSpawnIds[loot] = -1;
                }
                if (_lootSpawnIds[loot] != loot.SpawnId)
                {
                    _lootSpawnIds[loot] = loot.SpawnId;
                    ApplyLootText(loot.Entry, plate);
                }

                Vector3 world = loot.transform.position + Vector3.up * lootLabelHeight;
                Vector3 screen = viewCamera.WorldToScreenPoint(world);
                bool visible = loot.IsVisualVisible && screen.z > 0f && (world - camPos).sqrMagnitude <= sqrMax;
                if (plate.enabled != visible) plate.enabled = visible;
                if (!visible) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
                plate.rectTransform.anchoredPosition = local;
            }
        }

        private void ApplyLootText(in LootEntry entry, Text plate)
        {
            if (entry.IsGold)
            {
                plate.text = $"{entry.Gold:N0} Gold";
                plate.color = goldLabelColor;
                return;
            }
            plate.text = entry.Count > 1 ? $"{entry.Item.DisplayName} x{entry.Count}" : entry.Item.DisplayName;
            plate.color = ItemGradeColors.Text(entry.Item.Grade);
        }

        /// <summary>NPC 이름표 "[Merchant] 이름" (몬스터와 같은 풀 사용)</summary>
        private void UpdateNpcPlates(Vector3 camPos, float sqrMax)
        {
            _npcToRemove.Clear();
            foreach (var pair in _npcPlates)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled) _npcToRemove.Add(pair.Key);
            foreach (var n in _npcToRemove)
            {
                _pool.Release(_npcPlates[n]);
                _npcPlates.Remove(n);
            }

            foreach (var npc in NpcInteractable.Active)
            {
                if (!_npcPlates.TryGetValue(npc, out var plate))
                {
                    plate = _pool.Get();
                    plate.text = string.IsNullOrEmpty(npc.Title) ? npc.DisplayName : $"[{npc.Title}] {npc.DisplayName}";
                    plate.color = npcColor;
                    _npcPlates[npc] = plate;
                }

                Vector3 world = npc.HeadPosition;
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
