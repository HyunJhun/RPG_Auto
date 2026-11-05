using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Monster;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

namespace RPGAuto.Loot
{
    /// <summary>
    /// 몬스터 처치 이벤트를 구독(Observer)해 드랍을 처리한다.
    /// - 일반 몬스터: 처치한 플레이어에게 즉시 지급
    /// - 엘리트/보스: 처치 위치 주변 바닥에 GroundLoot 생성 (Object Pool)
    /// </summary>
    public class LootManager : MonoBehaviour
    {
        [SerializeField] private GroundLoot groundLootPrefab;
        [SerializeField] private float scatterRadius = 1.6f;
        [SerializeField] private float groundLifetime = 60f;
        [SerializeField] private int poolMaxSize = 64;

        private ObjectPool<GroundLoot> _pool;
        private readonly List<LootEntry> _buffer = new();

        private void Awake()
        {
            _pool = new ObjectPool<GroundLoot>(
                createFunc: () =>
                {
                    var loot = Instantiate(groundLootPrefab, transform);
                    loot.name = groundLootPrefab.name;
                    return loot;
                },
                actionOnGet: l => l.gameObject.SetActive(true),
                actionOnRelease: l => l.gameObject.SetActive(false),
                actionOnDestroy: l => { if (l != null) Destroy(l.gameObject); },
                collectionCheck: true, defaultCapacity: 16, maxSize: poolMaxSize);
        }

        private void OnEnable() => MonsterController.OnAnyKilled += HandleMonsterKilled;
        private void OnDisable() => MonsterController.OnAnyKilled -= HandleMonsterKilled;

        private void HandleMonsterKilled(MonsterController monster, CombatStats killer)
        {
            if (monster.DropTable == null || killer == null) return;
            var looter = killer.GetComponent<PlayerLooter>();
            if (looter == null) return; // 플레이어가 잡은 경우만

            _buffer.Clear();
            monster.DropTable.Roll(_buffer);
            if (_buffer.Count == 0) return;

            if (!monster.DropsOnGround)
            {
                foreach (var entry in _buffer) looter.Receive(entry);
                return;
            }

            Vector3 origin = monster.FootPosition;
            Vector3 from = monster.transform.position;
            float angleOffset = Random.Range(0f, 360f);
            for (int i = 0; i < _buffer.Count; i++)
            {
                // 처치 위치를 중심으로 고르게 흩뿌린다
                float angle = (angleOffset + 360f * i / _buffer.Count) * Mathf.Deg2Rad;
                float dist = _buffer.Count == 1 ? 0.5f : scatterRadius * Random.Range(0.6f, 1f);
                Vector3 to = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                if (NavMesh.SamplePosition(to, out var hit, 1.5f, NavMesh.AllAreas)) to = hit.position;
                else to = origin;

                _pool.Get().Init(this, _buffer[i], from, to, groundLifetime);
            }
        }

        internal void Release(GroundLoot loot) => _pool.Release(loot);
    }
}
