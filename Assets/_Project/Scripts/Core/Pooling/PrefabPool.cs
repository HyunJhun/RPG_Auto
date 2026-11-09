using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace RPGAuto.Core.Pooling
{
    /// <summary>
    /// 풀에서 꺼낼 때/반납할 때 자기 상태를 직접 초기화하고 싶은 오브젝트가 구현한다.
    /// PrefabPool이 루트 컴포넌트에 구현돼 있으면 자동으로 호출한다.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>활성화 직후 (위치는 이미 지정된 상태)</summary>
        void OnSpawned();

        /// <summary>비활성화 직전</summary>
        void OnDespawned();
    }

    /// <summary>
    /// 프리팹 기반 Object Pool 공통 래퍼 (Unity ObjectPool&lt;T&gt; 사용).
    /// - SetActive / 이름 정리 / 부모 지정 / IPoolable 호출을 한 곳에서 처리
    /// - Get(position, rotation): 위치를 먼저 잡고 활성화 → NavMeshAgent 경고 방지
    /// - 풀 인스턴스는 사용하는 쪽(스포너 등)이 직접 소유한다 (전역 싱글톤 아님)
    /// </summary>
    public class PrefabPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<T> _pool;
        private readonly Action<T> _onCreated;

        // Get(position)으로 요청된 배치 정보 (Create/OnGet 콜백에서 사용)
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation = Quaternion.identity;
        private bool _hasSpawnPose;

        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;
        public int CountAll => _pool.CountAll;

        /// <param name="onCreated">새로 만들어질 때 1회 호출 (이벤트 구독 등)</param>
        public PrefabPool(T prefab, Transform parent, int defaultCapacity = 10, int maxSize = 100, Action<T> onCreated = null)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            _prefab = prefab;
            _parent = parent;
            _onCreated = onCreated;

            _pool = new ObjectPool<T>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: Application.isEditor, // 중복 반납 검사는 에디터에서만 (빌드에서는 비용 절약)
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        // ---------------- Public API ----------------

        /// <summary>현재 위치 그대로 꺼낸다 (UI 등 위치를 따로 잡는 경우)</summary>
        public T Get()
        {
            _hasSpawnPose = false;
            return _pool.Get();
        }

        /// <summary>위치/회전을 먼저 지정한 뒤 활성화해서 꺼낸다</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
            _hasSpawnPose = true;
            try
            {
                return _pool.Get();
            }
            finally
            {
                _hasSpawnPose = false;
            }
        }

        public void Release(T item)
        {
            if (item != null) _pool.Release(item);
        }

        /// <summary>
        /// 미리 만들어 둔다. 내부적으로 Get → Release 하므로 OnSpawned/OnDespawned가 한 번씩 호출된다.
        /// (몬스터처럼 활성화 시 부작용이 있는 오브젝트는 Prewarm 대신 필요할 때 생성 권장)
        /// </summary>
        public void Prewarm(int count)
        {
            var temp = ListPool<T>.Get();
            for (int i = 0; i < count; i++) temp.Add(Get());
            foreach (var item in temp) Release(item);
            ListPool<T>.Release(temp);
        }

        public void Clear() => _pool.Clear();

        // ---------------- ObjectPool callbacks ----------------

        private T Create()
        {
            // 처음 생성될 때도 요청 위치에 만들어 Awake/OnEnable 시점부터 올바른 위치에 있게 한다
            T item = _hasSpawnPose
                ? Object.Instantiate(_prefab, _spawnPosition, _spawnRotation, _parent)
                : Object.Instantiate(_prefab, _parent);
            item.name = _prefab.name;
            item.gameObject.SetActive(false); // 활성화는 OnGet에서 한 번에
            _onCreated?.Invoke(item);
            return item;
        }

        private void OnGet(T item)
        {
            if (_hasSpawnPose) item.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            item.gameObject.SetActive(true);
            if (item is IPoolable poolable) poolable.OnSpawned();
        }

        private void OnRelease(T item)
        {
            if (item is IPoolable poolable) poolable.OnDespawned();
            item.gameObject.SetActive(false);
        }

        private static void OnDestroyItem(T item)
        {
            if (item != null) Object.Destroy(item.gameObject);
        }
    }
}
