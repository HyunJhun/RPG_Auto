using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

namespace RPGAuto.Monster
{
    /// <summary>영역 안에 몬스터를 유지하는 스포너 (Object Pool)</summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterController prefab;
        [SerializeField] private Vector2 areaSize = new(14f, 10f);
        [SerializeField] private int maxAlive = 5;
        [SerializeField] private float respawnDelay = 4f;

        private ObjectPool<MonsterController> _pool;
        private readonly List<float> _respawnTimes = new();
        private Vector3 _pendingPosition;

        public int AliveCount { get; private set; }

        private void Awake()
        {
            _pool = new ObjectPool<MonsterController>(
                createFunc: Create,
                actionOnGet: null,
                actionOnRelease: m => m.gameObject.SetActive(false),
                actionOnDestroy: m => { if (m != null) Destroy(m.gameObject); },
                collectionCheck: true,
                defaultCapacity: maxAlive,
                maxSize: maxAlive * 2);
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
            if (prefab == null) return;
            if (!TryGetSpawnPoint(out _pendingPosition))
            {
                _respawnTimes.Add(Time.time + 1f); // 자리가 없으면 잠시 후 재시도
                return;
            }

            var monster = _pool.Get();
            monster.transform.position = _pendingPosition;
            monster.gameObject.SetActive(true);
            monster.Spawn(_pendingPosition);
            AliveCount++;
        }

        private MonsterController Create()
        {
            // 생성 시점부터 NavMesh 위에 두어 NavMeshAgent 경고를 막는다
            var monster = Instantiate(prefab, _pendingPosition, Quaternion.identity, transform);
            monster.name = prefab.name;
            monster.OnDespawnRequested += HandleDespawn;
            return monster;
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
