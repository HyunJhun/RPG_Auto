using System.Collections.Generic;
using RPGAuto.Core.Pooling;
using UnityEngine;

namespace RPGAuto.Loot
{
    /// <summary>
    /// 바닥에 떨어진 전리품 (엘리트/보스 드랍). LootManager의 Object Pool에서 재사용된다.
    /// 줍기 판정은 PlayerLooter가 거리로 처리한다.
    /// </summary>
    public class GroundLoot : MonoBehaviour, IPoolable
    {
        private static readonly List<GroundLoot> s_active = new();
        public static IReadOnlyList<GroundLoot> Active => s_active;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer goldVisual;
        [SerializeField] private Renderer itemVisual;

        [Header("Motion")]
        [SerializeField] private float popDuration = 0.45f;
        [SerializeField] private float popHeight = 1.4f;
        [SerializeField] private float hoverHeight = 0.35f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float spinSpeed = 120f;
        [Tooltip("튀어나온 직후 바로 주워지지 않도록 대기")]
        [SerializeField] private float pickupDelay = 0.5f;
        [Tooltip("사라지기 전 깜빡이는 시간")]
        [SerializeField] private float blinkDuration = 5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;
        private LootManager _owner;
        private Vector3 _from;
        private Vector3 _to;
        private float _age;
        private float _lifetime;
        private bool _collected;

        public LootEntry Entry { get; private set; }
        /// <summary>바닥 위치 (발밑 기준)</summary>
        public Vector3 GroundPosition => _to;
        public bool IsAvailable => !_collected && _age >= pickupDelay && gameObject.activeInHierarchy;

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        internal void Init(LootManager owner, LootEntry entry, Vector3 from, Vector3 to, float lifetime)
        {
            _owner = owner;
            Entry = entry;
            _from = from;
            _to = to;
            _lifetime = lifetime;

            bool gold = entry.IsGold;
            if (goldVisual != null) goldVisual.enabled = gold;
            if (itemVisual != null)
            {
                itemVisual.enabled = !gold;
                if (!gold)
                {
                    _mpb ??= new MaterialPropertyBlock();
                    itemVisual.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColorId, entry.Item.SlotColor);
                    itemVisual.SetPropertyBlock(_mpb);
                }
            }

            transform.position = from;
        }

        public void OnSpawned()
        {
            _age = 0f;
            _collected = false;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true); // 깜빡임 도중 반납됐던 경우 대비
                visualRoot.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        public void OnDespawned() => _collected = true; // 반납 후 IsAvailable이 true로 보이지 않게

        /// <summary>플레이어가 주웠을 때</summary>
        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            _owner.Release(this);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                _collected = true;
                _owner.Release(this);
                return;
            }

            // 처치 위치에서 포물선으로 튀어나와 바닥에 안착
            Vector3 pos;
            if (_age < popDuration)
            {
                float t = _age / popDuration;
                pos = Vector3.Lerp(_from, _to, t);
                pos.y = Mathf.Lerp(_from.y, _to.y + hoverHeight, t) + popHeight * 4f * t * (1f - t);
            }
            else
            {
                pos = _to + Vector3.up * (hoverHeight + Mathf.Sin((_age - popDuration) * bobSpeed) * bobAmplitude);
            }
            transform.position = pos;

            if (visualRoot != null)
            {
                visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                // 사라지기 직전 깜빡임
                float remain = _lifetime - _age;
                bool visible = remain > blinkDuration || Mathf.Repeat(remain, 0.4f) > 0.15f;
                if (visualRoot.gameObject.activeSelf != visible) visualRoot.gameObject.SetActive(visible);
            }
        }
    }
}
