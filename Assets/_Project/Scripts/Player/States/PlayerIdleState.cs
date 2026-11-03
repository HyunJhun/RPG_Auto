using RPGAuto.Core;

namespace RPGAuto.Player.States
{
    public class PlayerIdleState : IState
    {
        private readonly PlayerController _p;
        public PlayerIdleState(PlayerController player) => _p = player;

        public void Enter()
        {
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
            // 직접 이동/터치 이동을 마친 자리가 새 사냥 기준점이 된다
            if (_p.Combat.IsAutoEnabled) _p.Combat.Anchor = _p.transform.position;
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput)
            {
                _p.StateMachine.ChangeState(_p.ManualMoveState);
                return;
            }
            // 지면을 누른 채 이동 중이면 자동 전투 재개를 미룬다
            if (_p.Combat.IsAutoEnabled && !_p.Input.IsHoldingGround)
                _p.StateMachine.ChangeState(_p.AutoSearchState);
        }

        public void Exit() { }
    }
}
