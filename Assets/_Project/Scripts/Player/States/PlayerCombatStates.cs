using RPGAuto.Combat.Targeting;
using RPGAuto.Core;
using UnityEngine;

namespace RPGAuto.Player.States
{
    /// <summary>자동 전투: 주변 탐지 → 대상이 없으면 사냥 기준점으로 복귀</summary>
    public class PlayerAutoSearchState : IState
    {
        private const float ScanInterval = 0.25f;
        private const float AnchorTolerance = 1.5f;
        private readonly PlayerController _p;
        private float _scanTimer;

        public PlayerAutoSearchState(PlayerController player) => _p = player;

        public void Enter()
        {
            _scanTimer = 0f;
            _p.IsManualEngage = false;
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput) { _p.StateMachine.ChangeState(_p.ManualMoveState); return; }
            if (!_p.Combat.IsAutoEnabled) { _p.StateMachine.ChangeState(_p.IdleState); return; }

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = ScanInterval;

            var target = _p.Combat.FindTarget(_p.transform.position, useLeash: true);
            if (target != null)
            {
                _p.Combat.SetTarget(target);
                _p.StateMachine.ChangeState(_p.ChaseState);
                return;
            }

            if (_p.FlatDistanceTo(_p.Combat.Anchor) > AnchorTolerance)
            {
                _p.AutoMoveState.SetDestination(_p.Combat.Anchor);
                _p.StateMachine.ChangeState(_p.AutoMoveState);
            }
        }

        public void Exit() { }
    }

    /// <summary>대상에게 교전 거리까지 접근</summary>
    public class PlayerChaseState : IState
    {
        private const float RepathInterval = 0.2f;
        private readonly PlayerController _p;
        private float _repath;

        public PlayerChaseState(PlayerController player) => _p = player;

        public void Enter()
        {
            _p.Agent.isStopped = false;
            _p.Agent.updateRotation = true;
            _repath = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput) { _p.StateMachine.ChangeState(_p.ManualMoveState); return; }

            var combat = _p.Combat;
            var target = combat.Target;
            if (target == null || !target.IsAlive) { _p.HandleTargetLost(); return; }

            // 자동 전투 중에는 기준점에서 너무 멀어진 대상을 포기 (수동 지정 대상은 예외)
            if (combat.IsAutoEnabled && !_p.IsManualEngage &&
                TargetQuery.FlatDistance(target.transform.position, combat.Anchor) > combat.Settings.returnDistance)
            {
                combat.SetTarget(null);
                _p.StateMachine.ChangeState(_p.AutoSearchState);
                return;
            }

            if (_p.FlatDistanceTo(target.transform.position) <= _p.GetEngageRange())
            {
                _p.StateMachine.ChangeState(_p.AttackState);
                return;
            }

            _repath -= deltaTime;
            if (_repath <= 0f && _p.Agent.isOnNavMesh)
            {
                _repath = RepathInterval;
                _p.Agent.SetDestination(target.transform.position);
            }
        }

        public void Exit()
        {
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
        }
    }

    /// <summary>교전: 예약 스킬 → 자동 스킬(조건 충족 시) → 일반 공격 순</summary>
    public class PlayerAttackState : IState
    {
        private const float RangeTolerance = 0.25f;
        private readonly PlayerController _p;

        public PlayerAttackState(PlayerController player) => _p = player;

        public void Enter()
        {
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
            _p.Agent.updateRotation = false;
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput) { _p.StateMachine.ChangeState(_p.ManualMoveState); return; }

            var combat = _p.Combat;
            var target = combat.Target;
            if (target == null || !target.IsAlive) { _p.HandleTargetLost(); return; }

            Vector3 to = target.transform.position - _p.transform.position;
            to.y = 0f;
            if (to.magnitude > _p.GetEngageRange() + RangeTolerance)
            {
                _p.StateMachine.ChangeState(_p.ChaseState);
                return;
            }

            _p.RotateTowards(to, deltaTime);

            var skills = _p.Skills;
            if (skills.IsActionLocked) return;

            var queued = combat.QueuedSkill;
            if (queued != null)
            {
                if (skills.TryUse(queued, target)) { combat.QueuedSkill = null; return; }
                if (!queued.IsReady || _p.Stats.Mp < queued.Data.MpCost) combat.QueuedSkill = null;
            }

            if (combat.IsAutoEnabled)
            {
                var auto = skills.SelectAutoSkill(target, combat.Settings.skillMpLimitPercent);
                if (auto != null && skills.TryUse(auto, target)) return;
            }

            skills.TryUse(skills.BasicAttack, target);
        }

        public void Exit() => _p.Agent.updateRotation = true;
    }

    /// <summary>사망 → 일정 시간 후 시작 지점에서 부활</summary>
    public class PlayerDeadState : IState
    {
        private readonly PlayerController _p;
        private float _timer;

        public PlayerDeadState(PlayerController player) => _p = player;

        public void Enter()
        {
            _timer = _p.RespawnDelay;
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
            _p.Agent.isStopped = true;
            _p.Agent.updateRotation = false;
            _p.Combat.SetTarget(null);
            _p.Combat.QueuedSkill = null;
            _p.IsManualEngage = false;
            _p.SetDeadVisual(true);
        }

        public void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer <= 0f) _p.Respawn();
        }

        public void Exit()
        {
            _p.Agent.isStopped = false;
            _p.Agent.updateRotation = true;
            _p.SetDeadVisual(false);
        }
    }
}
