using RPGAuto.Core;
using UnityEngine;

namespace RPGAuto.Player.States
{
    /// <summary>WASD / 가상 조이스틱 직접 이동 (자동 전투는 일시 중지, 손을 떼면 재개)</summary>
    public class PlayerManualMoveState : IState
    {
        private readonly PlayerController _p;
        public PlayerManualMoveState(PlayerController player) => _p = player;

        public void Enter()
        {
            var agent = _p.Agent;
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.updateRotation = false;
            _p.IsManualEngage = false;
            _p.Combat.QueuedSkill = null;
        }

        public void Tick(float deltaTime)
        {
            if (!_p.HasMoveInput)
            {
                _p.StateMachine.ChangeState(_p.IdleState);
                return;
            }

            Vector3 dir = _p.GetCameraRelativeDirection(_p.Input.MoveInput);
            if (_p.Agent.isOnNavMesh)
                _p.Agent.Move(dir * (_p.MoveSpeed * deltaTime));
            _p.RotateTowards(dir, deltaTime);
        }

        public void Exit() => _p.Agent.updateRotation = true;
    }
}
