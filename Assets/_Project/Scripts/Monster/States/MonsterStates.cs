using RPGAuto.Core;
using UnityEngine;

namespace RPGAuto.Monster.States
{
    public class MonsterIdleState : IState
    {
        private readonly MonsterController _m;
        public MonsterIdleState(MonsterController m) => _m = m;

        public void Enter()
        {
            var agent = _m.Agent;
            if (!agent.isOnNavMesh) return;
            // 리쉬로 돌아온 경우 스폰 지점으로 복귀
            if (_m.FlatDistanceTo(_m.HomePosition) > 0.5f) agent.SetDestination(_m.HomePosition);
            else agent.ResetPath();
        }

        public void Tick(float deltaTime)
        {
            if (_m.AggroTarget != null && !_m.AggroTarget.IsDead)
                _m.StateMachine.ChangeState(_m.ChaseState);
        }

        public void Exit() { }
    }

    public class MonsterChaseState : IState
    {
        private const float RepathInterval = 0.2f;
        private readonly MonsterController _m;
        private float _repath;

        public MonsterChaseState(MonsterController m) => _m = m;

        public void Enter()
        {
            _m.Agent.isStopped = false;
            _repath = 0f;
        }

        public void Tick(float deltaTime)
        {
            var target = _m.AggroTarget;
            if (target == null || target.IsDead || _m.FlatDistanceTo(_m.HomePosition) > _m.LeashDistance)
            {
                _m.AggroTarget = null;
                _m.StateMachine.ChangeState(_m.IdleState);
                return;
            }

            if (_m.FlatDistanceTo(target.transform.position) <= _m.AttackRange)
            {
                _m.StateMachine.ChangeState(_m.AttackState);
                return;
            }

            _repath -= deltaTime;
            if (_repath <= 0f && _m.Agent.isOnNavMesh)
            {
                _repath = RepathInterval;
                _m.Agent.SetDestination(target.transform.position);
            }
        }

        public void Exit()
        {
            if (_m.Agent.isOnNavMesh) _m.Agent.ResetPath();
        }
    }

    public class MonsterAttackState : IState
    {
        private const float WindUp = 0.35f;
        private const float RangeTolerance = 0.4f;
        private readonly MonsterController _m;
        private float _timer;

        public MonsterAttackState(MonsterController m) => _m = m;

        public void Enter()
        {
            if (_m.Agent.isOnNavMesh) _m.Agent.ResetPath();
            _m.Agent.updateRotation = false;
            _timer = WindUp;
        }

        public void Tick(float deltaTime)
        {
            var target = _m.AggroTarget;
            if (target == null || target.IsDead)
            {
                _m.AggroTarget = null;
                _m.StateMachine.ChangeState(_m.IdleState);
                return;
            }

            Vector3 to = target.transform.position - _m.transform.position;
            if (_m.FlatDistanceTo(target.transform.position) > _m.AttackRange + RangeTolerance)
            {
                _m.StateMachine.ChangeState(_m.ChaseState);
                return;
            }

            _m.RotateTowards(to, deltaTime);

            _timer -= deltaTime;
            if (_timer > 0f) return;
            _timer = _m.AttackInterval;
            RPGAuto.Combat.DamageCalculator.Attack(_m.Stats, target, RPGAuto.Combat.DamageType.Physical, _m.AttackMultiplier);
        }

        public void Exit() => _m.Agent.updateRotation = true;
    }

    public class MonsterDeadState : IState
    {
        private readonly MonsterController _m;
        private float _timer;

        public MonsterDeadState(MonsterController m) => _m = m;

        public void Enter()
        {
            if (_m.Agent.isOnNavMesh)
            {
                _m.Agent.ResetPath();
                _m.Agent.isStopped = true;
            }
            _m.AggroTarget = null;
            _m.SetColliderEnabled(false);
            _timer = _m.DespawnDelay;
        }

        public void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            float t = Mathf.Clamp01(1f - _timer / _m.DespawnDelay);
            _m.transform.localScale = Vector3.Lerp(_m.BaseScale, _m.BaseScale * 0.1f, t);

            if (_timer <= 0f) _m.RequestDespawn();
        }

        public void Exit()
        {
            _m.transform.localScale = _m.BaseScale;
            if (_m.Agent.isOnNavMesh) _m.Agent.isStopped = false;
        }
    }
}
