using System;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Core;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Loot;
using RPGAuto.Monster;
using RPGAuto.Npc;
using RPGAuto.Player.States;
using RPGAuto.QuickSlots;
using RPGAuto.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Player
{
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatStats), typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerSkillController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Renderer bodyRenderer;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotateSpeed = 720f;
        [SerializeField] private float acceleration = 60f;
        [SerializeField] private float arriveThreshold = 0.05f;
        [SerializeField] private float tapSampleRadius = 3f;
        [SerializeField, Range(0f, 0.5f)] private float moveInputDeadZone = 0.1f;
        [Tooltip("무게 초과 시 이동 속도 배율")]
        [SerializeField, Range(0.1f, 1f)] private float overweightSpeedMultiplier = 0.5f;

        [Header("Death")]
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private Color deadColor = new(0.35f, 0.35f, 0.35f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;
        private PlayerInventory _inventory;
        private float _speedMultiplier = 1f;

        public NavMeshAgent Agent { get; private set; }
        public CombatStats Stats { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public PlayerSkillController Skills { get; private set; }
        public PlayerInputHandler Input => input;
        public QuickSlotBar QuickSlots { get; private set; }
        public PlayerLooter Looter { get; private set; }

        /// <summary>무게 초과 패널티가 반영된 이동 속도</summary>
        public float MoveSpeed => moveSpeed * _speedMultiplier;
        public bool IsOverweight => _inventory != null && _inventory.IsOverweight;
        public float ArriveThreshold => arriveThreshold;
        public float RespawnDelay => respawnDelay;
        public bool IsDead => Stats.IsDead;
        public bool HasMoveInput => input != null && input.MoveInput.sqrMagnitude > moveInputDeadZone * moveInputDeadZone;

        /// <summary>플레이어가 직접 지정한 대상과 교전 중 (자동 전투 리쉬 무시, 처치 후 자동 탐색 안 함)</summary>
        public bool IsManualEngage { get; internal set; }
        public Vector3 SpawnPosition { get; private set; }

        public StateMachine StateMachine { get; private set; }
        public PlayerIdleState IdleState { get; private set; }
        public PlayerManualMoveState ManualMoveState { get; private set; }
        public PlayerAutoMoveState AutoMoveState { get; private set; }
        public PlayerAutoSearchState AutoSearchState { get; private set; }
        public PlayerChaseState ChaseState { get; private set; }
        public PlayerAttackState AttackState { get; private set; }
        public PlayerLootState LootState { get; private set; }
        public PlayerInteractState InteractState { get; private set; }
        public PlayerDeadState DeadState { get; private set; }

        // Observer
        public event Action<Vector3> OnDestinationSet;
        public event Action OnDestinationCleared;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Stats = GetComponent<CombatStats>();
            Combat = GetComponent<PlayerCombat>();
            Skills = GetComponent<PlayerSkillController>();
            QuickSlots = GetComponent<QuickSlotBar>();
            Looter = GetComponent<PlayerLooter>();
            _inventory = GetComponent<PlayerInventory>();
            if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            Agent.speed = moveSpeed;
            Agent.acceleration = acceleration;
            Agent.angularSpeed = rotateSpeed;
            Agent.stoppingDistance = 0f;
            Agent.autoBraking = true;

            if (viewCamera == null) viewCamera = Camera.main;

            StateMachine = new StateMachine();
            IdleState = new PlayerIdleState(this);
            ManualMoveState = new PlayerManualMoveState(this);
            AutoMoveState = new PlayerAutoMoveState(this);
            AutoSearchState = new PlayerAutoSearchState(this);
            ChaseState = new PlayerChaseState(this);
            AttackState = new PlayerAttackState(this);
            LootState = new PlayerLootState(this);
            InteractState = new PlayerInteractState(this);
            DeadState = new PlayerDeadState(this);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.OnGroundTapped += HandleGroundTapped;
                input.OnTargetTapped += HandleTargetTapped;
                input.OnAttackPressed += HandleAttackPressed;
                input.OnQuickSlotPressed += HandleQuickSlotPressed;
                input.OnAutoCombatTogglePressed += HandleAutoTogglePressed;
                input.OnNpcTapped += HandleNpcTapped;
                input.OnInteractPressed += HandleInteractPressed;
            }
            Combat.OnAutoCombatChanged += HandleAutoCombatChanged;
            Stats.OnDied += HandleDied;
            if (_inventory != null) _inventory.OnWeightChanged += HandleWeightChanged;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.OnGroundTapped -= HandleGroundTapped;
                input.OnTargetTapped -= HandleTargetTapped;
                input.OnAttackPressed -= HandleAttackPressed;
                input.OnQuickSlotPressed -= HandleQuickSlotPressed;
                input.OnAutoCombatTogglePressed -= HandleAutoTogglePressed;
                input.OnNpcTapped -= HandleNpcTapped;
                input.OnInteractPressed -= HandleInteractPressed;
            }
            Combat.OnAutoCombatChanged -= HandleAutoCombatChanged;
            Stats.OnDied -= HandleDied;
            if (_inventory != null) _inventory.OnWeightChanged -= HandleWeightChanged;
        }

        private void Start()
        {
            SpawnPosition = transform.position;
            ApplyWeightPenalty();
            StateMachine.ChangeState(IdleState);
        }

        private void Update()
        {
            StateMachine.Tick(Time.deltaTime);

            if (!IsDead && Combat.IsAutoEnabled)
                Skills.TryAutoUseSelfSkills(Combat.Settings.skillMpLimitPercent);
        }

        // ---------------- Input handlers ----------------

        private void HandleGroundTapped(Vector3 worldPoint)
        {
            if (IsDead) return;
            if (!NavMesh.SamplePosition(worldPoint, out var hit, tapSampleRadius, NavMesh.AllAreas)) return;

            IsManualEngage = false;
            Combat.QueuedSkill = null;
            AutoMoveState.SetDestination(hit.position);
            StateMachine.ChangeState(AutoMoveState);
        }

        private void HandleTargetTapped(MonsterController monster) => EngageTarget(monster, manual: true);

        private void HandleAttackPressed()
        {
            if (IsDead) return;
            var target = Combat.HasValidTarget ? Combat.Target : Combat.FindTarget(transform.position, useLeash: false);
            EngageTarget(target, manual: true);
        }

        private void HandleQuickSlotPressed(int index)
        {
            if (IsDead || QuickSlots == null) return;
            QuickSlots.Activate(index);
        }

        /// <summary>스킬 수동 사용: 자기 대상은 즉시, 대상 지정은 사거리까지 접근 후 사용</summary>
        public void RequestSkill(SkillSlot slot)
        {
            if (IsDead || slot == null) return;

            if (!slot.Data.RequiresTarget)
            {
                Skills.TryUse(slot, null);
                return;
            }

            var target = Combat.HasValidTarget ? Combat.Target : Combat.FindTarget(transform.position, useLeash: false);
            if (target == null) return;

            Combat.QueuedSkill = slot;
            if (StateMachine.Current == AttackState && Combat.Target == target)
            {
                IsManualEngage = true;
                return;
            }
            EngageTarget(target, manual: true);
        }

        private void HandleAutoTogglePressed() => Combat.ToggleAutoCombat();

        /// <summary>NPC 탭: 범위 안이면 바로 말 걸고, 아니면 길찾기로 다가간다</summary>
        public void HandleNpcTapped(NpcInteractable npc)
        {
            if (IsDead || npc == null) return;
            Combat.SetTarget(null);
            Combat.QueuedSkill = null;
            IsManualEngage = false;
            if (npc.IsInRange(transform.position))
            {
                StateMachine.ChangeState(IdleState);
                npc.Interact(this);
                return;
            }
            InteractState.SetTarget(npc);
            StateMachine.ChangeState(InteractState);
        }

        /// <summary>E키 / 상호작용 버튼: 범위 안의 가장 가까운 NPC에게 말 걸기</summary>
        private void HandleInteractPressed()
        {
            if (IsDead) return;
            var npc = NpcInteractable.FindNearestInRange(transform.position);
            if (npc != null) npc.Interact(this);
        }

        // ---------------- Combat flow ----------------

        public void EngageTarget(MonsterController target, bool manual)
        {
            if (IsDead || target == null || !target.IsAlive) return;
            Combat.SetTarget(target);
            IsManualEngage = manual;
            if (StateMachine.Current == ChaseState || StateMachine.Current == AttackState) return;
            StateMachine.ChangeState(ChaseState);
        }

        /// <summary>대상 처치/소실 시: 자동 전투면 다음 대상 탐색, 아니면 대기</summary>
        public void HandleTargetLost()
        {
            Combat.SetTarget(null);
            Combat.QueuedSkill = null;
            IsManualEngage = false;
            StateMachine.ChangeState(Combat.IsAutoEnabled ? AutoSearchState : IdleState);
        }

        public float GetEngageRange()
        {
            var queued = Combat.QueuedSkill;
            if (queued != null && queued.Data.RequiresTarget) return queued.Data.Range;
            if (Combat.IsAutoEnabled) return Skills.GetAutoEngageRange(Combat.Settings.skillMpLimitPercent);
            return Skills.BasicAttack != null ? Skills.BasicAttack.Data.Range : 2f;
        }

        private void HandleAutoCombatChanged(bool enabled)
        {
            if (enabled || IsDead) return;

            var current = StateMachine.Current;
            bool autoDriven = current == AutoSearchState || current == LootState ||
                              ((current == ChaseState || current == AttackState) && !IsManualEngage);
            if (!autoDriven) return;

            Combat.SetTarget(null);
            StateMachine.ChangeState(IdleState);
        }

        private void HandleDied() => StateMachine.ChangeState(DeadState);

        private void HandleWeightChanged(float current, float max) => ApplyWeightPenalty();

        /// <summary>무게 초과면 이동 속도 감소 (NavMesh 이동·직접 이동 모두 MoveSpeed 사용)</summary>
        private void ApplyWeightPenalty()
        {
            _speedMultiplier = IsOverweight ? overweightSpeedMultiplier : 1f;
            Agent.speed = MoveSpeed;
        }

        public void Respawn()
        {
            Agent.Warp(SpawnPosition);
            Stats.ResetFull();
            StateMachine.ChangeState(IdleState);
        }

        // ---------------- Helpers ----------------

        public float FlatDistanceTo(Vector3 position) => TargetQuery.FlatDistance(transform.position, position);

        public Vector3 GetCameraRelativeDirection(Vector2 moveInput)
        {
            Transform cam = viewCamera != null ? viewCamera.transform : null;
            Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f)
                forward = cam != null ? Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
        }

        public void RotateTowards(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), rotateSpeed * deltaTime);
        }

        public void SetDeadVisual(bool dead)
        {
            if (bodyRenderer == null) return;
            if (dead)
            {
                bodyRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, deadColor);
                bodyRenderer.SetPropertyBlock(_mpb);
            }
            else
            {
                bodyRenderer.SetPropertyBlock(null);
            }
        }

        internal void NotifyDestinationSet(Vector3 destination) => OnDestinationSet?.Invoke(destination);
        internal void NotifyDestinationCleared() => OnDestinationCleared?.Invoke();
    }
}
