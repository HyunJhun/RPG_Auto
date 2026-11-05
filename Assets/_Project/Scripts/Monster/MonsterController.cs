using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Core;
using RPGAuto.Data;
using RPGAuto.Monster.States;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Monster
{
    public enum MonsterGrade
    {
        Normal,
        Elite,
        Boss
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(CombatStats))]
    public class MonsterController : MonoBehaviour
    {
        private static readonly List<MonsterController> s_active = new();
        /// <summary>현재 활성 몬스터 목록 (타겟 탐지/이름표용)</summary>
        public static IReadOnlyList<MonsterController> Active => s_active;

        /// <summary>몬스터 처치 알림 (몬스터, 처치자) — 경험치/퀘스트 등이 구독 (Observer)</summary>
        public static event Action<MonsterController, CombatStats> OnAnyKilled;

        [Header("Data (Excel: Monster 시트)")]
        [SerializeField] private StatDatabase database;
        [SerializeField] private string monsterId;

        [Header("Fallback (테이블에 없을 때)")]
        [SerializeField] private string displayName = "Monster";
        [SerializeField] private MonsterGrade grade = MonsterGrade.Normal;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackInterval = 1.6f;
        [SerializeField] private long expReward = 10;

        [Header("Loot")]
        [Tooltip("일반 = 처치 즉시 자동 획득, 엘리트/보스 = 바닥 드랍")]
        [SerializeField] private Loot.DropTable dropTable;

        [Header("AI")]
        [SerializeField] private float attackMultiplier = 1f;
        [Tooltip("스폰 위치에서 이 거리 이상 끌려가면 추적 포기 후 복귀")]
        [SerializeField] private float leashDistance = 14f;
        [SerializeField] private float despawnDelay = 1.2f;

        [Header("Visual")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.08f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Collider _collider;
        private MaterialPropertyBlock _mpb;
        private Color _baseColor = Color.white;
        private float _flashTimer;
        private Vector3 _baseScale;

        public string MonsterId => monsterId;
        public string DisplayName => displayName;
        public MonsterGrade Grade => grade;
        public int Level => Stats.Level;
        public long ExpReward => expReward;
        public Loot.DropTable DropTable => dropTable;
        /// <summary>엘리트/보스는 전리품을 바닥에 떨어뜨린다 (일반은 즉시 획득)</summary>
        public bool DropsOnGround => grade != MonsterGrade.Normal;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float AttackMultiplier => attackMultiplier;
        public float LeashDistance => leashDistance;
        public float DespawnDelay => despawnDelay;
        public Vector3 BaseScale => _baseScale;

        public NavMeshAgent Agent { get; private set; }
        public CombatStats Stats { get; private set; }
        public CombatStats AggroTarget { get; set; }
        public Vector3 HomePosition { get; private set; }
        public bool IsAlive => Stats != null && !Stats.IsDead && gameObject.activeInHierarchy;

        /// <summary>발밑 좌표 (Capsule 피벗이 중앙이므로 보정)</summary>
        public Vector3 FootPosition => transform.position - Vector3.up * (Agent.baseOffset * transform.lossyScale.y);
        /// <summary>머리 위 좌표 (이름표 위치)</summary>
        public Vector3 HeadPosition => transform.position + Vector3.up * (Agent.baseOffset * transform.lossyScale.y + 0.4f);

        public StateMachine StateMachine { get; private set; }
        public MonsterIdleState IdleState { get; private set; }
        public MonsterChaseState ChaseState { get; private set; }
        public MonsterAttackState AttackState { get; private set; }
        public MonsterDeadState DeadState { get; private set; }

        /// <summary>사망 연출 종료 후 풀 반환 요청 (Observer)</summary>
        public event Action<MonsterController> OnDespawnRequested;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Stats = GetComponent<CombatStats>();
            _collider = GetComponent<Collider>();
            _baseScale = transform.localScale;

            ApplyTableData();

            Agent.speed = moveSpeed;
            Agent.angularSpeed = 540f;
            Agent.acceleration = 30f;
            Agent.stoppingDistance = 0f;

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);

            StateMachine = new StateMachine();
            IdleState = new MonsterIdleState(this);
            ChaseState = new MonsterChaseState(this);
            AttackState = new MonsterAttackState(this);
            DeadState = new MonsterDeadState(this);

            Stats.OnDamaged += HandleDamaged;
            Stats.OnMissed += HandleMissed;
            Stats.OnDied += HandleDied;
        }

        /// <summary>엑셀 Monster 시트 데이터를 적용</summary>
        private void ApplyTableData()
        {
            if (database == null || string.IsNullOrEmpty(monsterId)) return;
            var row = database.GetMonster(monsterId);
            if (row == null) return;

            displayName = row.displayName;
            grade = row.grade;
            moveSpeed = row.moveSpeed;
            attackRange = row.attackRange;
            attackInterval = row.attackInterval;
            expReward = row.expReward;
            Stats.ApplyStats(row.level, row.stats, database.StatFormula, database.CombatFormula, refill: true);
        }

        private void OnDestroy()
        {
            if (Stats == null) return;
            Stats.OnDamaged -= HandleDamaged;
            Stats.OnMissed -= HandleMissed;
            Stats.OnDied -= HandleDied;
        }

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        /// <summary>풀에서 꺼낼 때 호출</summary>
        public void Spawn(Vector3 position)
        {
            transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            transform.localScale = _baseScale;
            Agent.Warp(position);
            HomePosition = position;
            Agent.isStopped = false;
            Agent.updateRotation = true;
            if (_collider != null) _collider.enabled = true;

            Stats.ResetFull();
            AggroTarget = null;
            _flashTimer = 0f;
            ApplyColor(_baseColor);

            StateMachine.ChangeState(IdleState);
        }

        private void Update()
        {
            StateMachine.Tick(Time.deltaTime);

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) ApplyColor(_baseColor);
            }
        }

        private void HandleDamaged(float amount, CombatStats attacker)
        {
            _flashTimer = hitFlashDuration;
            ApplyColor(hitFlashColor);
            Aggro(attacker);
        }

        private void HandleMissed(CombatStats attacker) => Aggro(attacker);

        private void Aggro(CombatStats attacker)
        {
            if (attacker == null || Stats.IsDead) return;
            AggroTarget = attacker;
            if (StateMachine.Current == IdleState) StateMachine.ChangeState(ChaseState);
        }

        private void HandleDied()
        {
            StateMachine.ChangeState(DeadState);
            OnAnyKilled?.Invoke(this, Stats.LastAttacker);
        }

        internal void RequestDespawn() => OnDespawnRequested?.Invoke(this);

        internal void SetColliderEnabled(bool enabled)
        {
            if (_collider != null) _collider.enabled = enabled;
        }

        public float FlatDistanceTo(Vector3 position) => TargetQuery.FlatDistance(transform.position, position);

        public void RotateTowards(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540f * deltaTime);
        }

        private void ApplyColor(Color color)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            bodyRenderer.SetPropertyBlock(_mpb);
        }
    }
}
