using RPGAuto.Core;
using RPGAuto.Loot;

namespace RPGAuto.Player.States
{
    /// <summary>자동 전투: 바닥 전리품으로 이동 (줍기는 PlayerLooter 반경 판정)</summary>
    public class PlayerLootState : IState
    {
        private const float RepathInterval = 0.25f;
        private const float GiveUpTime = 6f;
        private readonly PlayerController _p;
        private GroundLoot _target;
        private float _repath;
        private float _elapsed;

        public PlayerLootState(PlayerController player) => _p = player;

        public void SetTarget(GroundLoot loot) => _target = loot;

        public void Enter()
        {
            _p.Agent.isStopped = false;
            _p.Agent.updateRotation = true;
            _repath = 0f;
            _elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput) { _p.StateMachine.ChangeState(_p.ManualMoveState); return; }
            if (!_p.Combat.IsAutoEnabled) { _p.StateMachine.ChangeState(_p.IdleState); return; }

            _elapsed += deltaTime;
            // 이미 주웠거나(풀 반환) 사라졌거나, 길이 막혀 오래 걸리면 다시 탐색
            if (_target == null || !_target.IsAvailable || _elapsed > GiveUpTime)
            {
                _p.StateMachine.ChangeState(_p.AutoSearchState);
                return;
            }

            _repath -= deltaTime;
            if (_repath <= 0f && _p.Agent.isOnNavMesh)
            {
                _repath = RepathInterval;
                _p.Agent.SetDestination(_target.GroundPosition);
            }
        }

        public void Exit()
        {
            _target = null;
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
        }
    }
}
