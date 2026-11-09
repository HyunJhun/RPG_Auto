using System.Collections.Generic;
using RPGAuto.Core.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Monster
{
    /// <summary>영역 안에 몬스터를 유지하는 스포너 (PrefabPool)</summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterController prefab;
        [SerializeField] private Vector2 areaSize = new(14f, 10f);
        [SerializeField] private int maxAlive = 5;
        [SerializeField] private float respawnDelay = 4f;

        private PrefabPool<MonsterController> _pool;
        private readonly List<float> _respawnTimes = new();

        public int AliveCount { get; private set; }

        private void Awake()
        {
            if (prefab == null) return;
            _pool = new PrefabPool<MonsterController>(prefab, transform, maxAlive, maxAlive * 2,
                onCreated: m => m.OnDespawnRequested += HandleDespawn);
        }

        private void Start()
        {
            for (int i = 0; i < maxAlive; i++) SpawnOne();
        }

        private void Update()
        {
            for (int i = _respawnTimes.Count - 1; i >= 0; i--)
            {
                if (Time.time < _respawnTimes[i]) continue;
                _respawnTimes.RemoveAt(i);
                SpawnOne();
            }
        }

        private void SpawnOne()
        {
            if (_pool == null) return;
            if (!TryGetSpawnPoint(out var point))
            {
                _respawnTimes.Add(Time.time + 1f); // 자리가 없으면 잠시 후 재시도
                return;
            }

            // 위치 지정 → 활성화 → IPoolable.OnSpawned(초기화) 순서는 PrefabPool이 보장
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            _pool.Get(point, rotation);
            AliveCount++;
        }

        private void HandleDespawn(MonsterController monster)
        {
            _pool.Release(monster);
            AliveCount--;
            _respawnTimes.Add(Time.time + respawnDelay);
        }

        private bool TryGetSpawnPoint(out Vector3 point)
        {
            for (int i = 0; i < 15; i++)
            {
                Vector3 candidate = transform.position + new Vector3(
                    Random.Range(-areaSize.x, areaSize.x) * 0.5f, 0f,
                    Random.Range(-areaSize.y, areaSize.y) * 0.5f);

                if (NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = default;
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.2f, areaSize.y));
        }
    }
}
