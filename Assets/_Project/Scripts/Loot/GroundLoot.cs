using System.Collections.Generic;
using RPGAuto.Core.Pooling;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Loot
{
    /// <summary>
    /// 바닥에 떨어진 전리품 (엘리트/보스 드랍). LootManager의 Object Pool에서 재사용된다.
    /// 흐름: 튀어나와 착지 → 잠깐 보여 줌(이름표) → 빛나는 점으로 바뀌어 트레일을 남기며 주인 몸 중심으로 날아가 자동 획득.
    /// 가방이 가득 차 받을 수 없으면 바닥에 남고, 그때만 PlayerLooter의 반경 줍기 대상이 된다.
    /// </summary>
    public class GroundLoot : MonoBehaviour, IPoolable
    {
        private enum Phase { Pop, Show, Fly, Grounded }

        private static readonly List<GroundLoot> s_active = new();
        public static IReadOnlyList<GroundLoot> Active => s_active;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer goldVisual;
        [SerializeField] private Renderer itemVisual;

        [Header("Fly Visual (빛나는 점 + 트레일)")]
        [SerializeField] private Transform flyVisual;
        [SerializeField] private Renderer[] orbRenderers;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Color goldGlowColor = new(1f, 0.8f, 0.2f);

        [Header("Motion")]
        [SerializeField] private float popDuration = 0.45f;
        [SerializeField] private float popHeight = 1.4f;
        [SerializeField] private float hoverHeight = 0.35f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float spinSpeed = 120f;

        [Header("Auto Collect")]
        [Tooltip("착지 후 아이템을 보여 주는 시간(초). 이후 주인에게 날아간다")]
        [SerializeField] private float showDuration = 1f;
        [Tooltip("날아갈 때 위로 휘는 높이 (곡선 조절점)")]
        [SerializeField] private float arcHeight = 1.6f;
        [Tooltip("날아갈 때 옆으로 휘는 정도")]
        [SerializeField] private float arcSide = 1.2f;
        [Tooltip("거리 대비 비행 속도 (비행 시간 = 거리 / 속도, 아래 범위로 제한)")]
        [SerializeField] private float flySpeed = 14f;
        [SerializeField] private float minFlyTime = 0.35f;
        [SerializeField] private float maxFlyTime = 0.8f;
        [Tooltip("받을 수 없어 바닥에 남았을 때 다시 시도하는 주기(초)")]
        [SerializeField] private float retryInterval = 0.5f;

        [Header("Ground (fallback)")]
        [Tooltip("사라지기 전 깜빡이는 시간")]
        [SerializeField] private float blinkDuration = 5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;
        private LootManager _owner;
        private PlayerLooter _looter;
        private Vector3 _from;
        private Vector3 _to;
        private Vector3 _flyStart;
        private Vector3 _flyControl;
        private float _flyDuration;
        private float _age;
        private float _phaseTime;
        private float _lifetime;
        private float _nextRetry;
        private bool _collected;
        private Phase _phase;

        public LootEntry Entry { get; private set; }
        /// <summary>풀에서 꺼낼 때마다 증가 (재사용된 개체 구분용)</summary>
        public int SpawnId { get; private set; }
        /// <summary>바닥 위치 (발밑 기준)</summary>
        public Vector3 GroundPosition => _to;
        /// <summary>아이템 모습이 보이는 상태인지 (날아가는 중/깜빡임 꺼짐이면 false → 이름표도 숨김)</summary>
        public bool IsVisualVisible => !_collected && _phase != Phase.Fly && (visualRoot == null || visualRoot.gameObject.activeSelf);
        /// <summary>직접 줍기 대상인지 (자동 획득에 실패해 바닥에 남은 경우만)</summary>
        public bool IsAvailable => !_collected && _phase == Phase.Grounded && gameObject.activeInHierarchy;

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        internal void Init(LootManager owner, PlayerLooter looter, LootEntry entry, Vector3 from, Vector3 to, float lifetime)
        {
            _owner = owner;
            _looter = looter;
            Entry = entry;
            _from = from;
            _to = to;
            _lifetime = lifetime;

            bool gold = entry.IsGold;
            if (goldVisual != null) goldVisual.enabled = gold;
            if (itemVisual != null)
            {
                itemVisual.enabled = !gold;
                if (!gold) SetColor(itemVisual, entry.Item.SlotColor);
            }

            // 날아갈 때의 점/트레일 색 = 등급 색 (골드는 금색)
            Color glow = gold ? goldGlowColor : ItemGradeColors.Text(entry.Item.Grade);
            if (orbRenderers != null)
                for (int i = 0; i < orbRenderers.Length; i++)
                {
                    var c = glow;
                    c.a = i == 0 ? 1f : 0.35f; // 0 = 중심, 나머지 = 은은한 광륜
                    SetColor(orbRenderers[i], c);
                }
            if (trail != null)
            {
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(glow, 0.25f), new GradientColorKey(glow, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
            }

            transform.position = from;
        }

        public void OnSpawned()
        {
            SpawnId++;
            _age = 0f;
            _phaseTime = 0f;
            _phase = Phase.Pop;
            _collected = false;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true); // 깜빡임/비행 도중 반납됐던 경우 대비
                visualRoot.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            SetFlyVisual(false);
        }

        public void OnDespawned()
        {
            _collected = true; // 반납 후 IsAvailable이 true로 보이지 않게
            SetFlyVisual(false);
        }

        /// <summary>플레이어가 직접 주웠을 때 (바닥에 남은 경우)</summary>
        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            _owner.Release(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            _phaseTime += dt;

            switch (_phase)
            {
                case Phase.Pop: TickPop(); break;
                case Phase.Show: TickHover(); if (_phaseTime >= showDuration) TryStartFly(); break;
                case Phase.Fly: TickFly(dt); break;
                case Phase.Grounded: TickGrounded(); break;
            }
        }

        // 처치 위치에서 포물선으로 튀어나와 바닥에 안착
        private void TickPop()
        {
            float t = Mathf.Clamp01(_age / popDuration);
            var pos = Vector3.Lerp(_from, _to, t);
            pos.y = Mathf.Lerp(_from.y, _to.y + hoverHeight, t) + popHeight * 4f * t * (1f - t);
            transform.position = pos;
            Spin();
            if (t >= 1f) ChangePhase(Phase.Show);
        }

        private void TickHover()
        {
            transform.position = _to + Vector3.up * (hoverHeight + Mathf.Sin((_age - popDuration) * bobSpeed) * bobAmplitude);
            Spin();
        }

        private void TryStartFly()
        {
            if (!CanFlyToOwner())
            {
                ChangePhase(Phase.Grounded);
                _nextRetry = Time.time + retryInterval;
                return;
            }

            ChangePhase(Phase.Fly);
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            SetFlyVisual(true);
            // 시작점 → (위·옆으로 휜 조절점) → 주인 몸 중심, 2차 베지어 곡선으로 휘어 들어간다
            _flyStart = transform.position;
            Vector3 toTarget = _looter.BodyCenter - _flyStart;
            Vector3 side = Vector3.Cross(Vector3.up, toTarget).normalized * (Random.value < 0.5f ? -1f : 1f);
            _flyControl = _flyStart + toTarget * 0.25f + Vector3.up * arcHeight + side * (arcSide * Random.Range(0.3f, 1f));
            _flyDuration = Mathf.Clamp(toTarget.magnitude / flySpeed, minFlyTime, maxFlyTime);
        }

        private void TickFly(float dt)
        {
            if (_looter == null || !_looter.isActiveAndEnabled) { Drop(); return; }

            // 끝점이 움직이는 캐릭터를 따라가므로 정해진 시간 안에 반드시 도착한다
            float t = Mathf.Clamp01(_phaseTime / _flyDuration);
            float e = t * t * (2f - t) * 0.5f + t * t * 0.5f; // 처음엔 천천히, 끝으로 갈수록 빠르게
            Vector3 target = _looter.BodyCenter;
            float u = 1f - e;
            transform.position = u * u * _flyStart + 2f * u * e * _flyControl + e * e * target;
            if (t >= 1f) Arrive();
        }

        private void Arrive()
        {
            if (_collected) return;
            if (_looter.Receive(Entry))
            {
                _collected = true;
                _owner.Release(this);
            }
            else Drop(); // 날아오는 사이 가방이 찼으면 바닥에 다시 떨어뜨린다
        }

        /// <summary>자동 획득 실패: 원래 자리 바닥에 남긴다</summary>
        private void Drop()
        {
            SetFlyVisual(false);
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            ChangePhase(Phase.Grounded);
            _nextRetry = Time.time + retryInterval;
        }

        private void TickGrounded()
        {
            if (_age >= _lifetime)
            {
                _collected = true;
                _owner.Release(this);
                return;
            }

            TickHover();

            // 가방에 자리가 생기면 다시 날아간다
            if (Time.time >= _nextRetry)
            {
                _nextRetry = Time.time + retryInterval;
                if (CanFlyToOwner()) { TryStartFly(); return; }
            }

            // 사라지기 직전 깜빡임
            if (visualRoot != null)
            {
                float remain = _lifetime - _age;
                bool visible = remain > blinkDuration || Mathf.Repeat(remain, 0.4f) > 0.15f;
                if (visualRoot.gameObject.activeSelf != visible) visualRoot.gameObject.SetActive(visible);
            }
        }

        private bool CanFlyToOwner() =>
            _looter != null && _looter.isActiveAndEnabled && !_looter.IsDead && _looter.CanReceive(Entry);

        private void ChangePhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void Spin()
        {
            if (visualRoot != null) visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void SetFlyVisual(bool on)
        {
            if (flyVisual != null) flyVisual.gameObject.SetActive(on);
            if (trail != null)
            {
                trail.Clear(); // 이전 위치에서 선이 이어지지 않게
                trail.emitting = on;
            }
        }

        private void SetColor(Renderer r, Color color)
        {
            if (r == null) return;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            r.SetPropertyBlock(_mpb);
        }
    }
}
