using RPGAuto.Core;
using RPGAuto.Npc;

namespace RPGAuto.Player.States
{
    /// <summary>NPC를 탭했을 때: 상호작용 범위까지 길찾기 이동 → 말 걸기 → 대기</summary>
    public class PlayerInteractState : IState
    {
        private const float RepathInterval = 0.3f;
        private readonly PlayerController _p;
        private NpcInteractable _target;
        private float _repath;

        public PlayerInteractState(PlayerController player) => _p = player;

        public NpcInteractable Target => _target;

        public void SetTarget(NpcInteractable npc) => _target = npc;

        public void Enter()
        {
            _p.Agent.isStopped = false;
            _p.Agent.updateRotation = true;
            _repath = 0f;
            if (_target != null) _p.NotifyDestinationSet(_target.Position);
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput) { _p.StateMachine.ChangeState(_p.ManualMoveState); return; }
            if (_target == null || !_target.isActiveAndEnabled) { _p.StateMachine.ChangeState(_p.IdleState); return; }

            // 범위 살짝 안쪽까지 들어가면 말을 건다
            if (_target.IsInRange(_p.transform.position))
            {
                var npc = _target;
                _p.StateMachine.ChangeState(_p.IdleState);
                npc.Interact(_p);
                return;
            }

            _repath -= deltaTime;
            if (_repath <= 0f && _p.Agent.isOnNavMesh)
            {
                _repath = RepathInterval;
                _p.Agent.SetDestination(_target.Position);
            }
        }

        public void Exit()
        {
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
            _p.NotifyDestinationCleared();
        }
    }
}
