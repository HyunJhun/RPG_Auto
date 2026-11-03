using RPGAuto.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Player.States
{
    /// <summary>터치/클릭 지점 또는 사냥 기준점으로 NavMesh 길찾기 이동</summary>
    public class PlayerAutoMoveState : IState
    {
        private readonly PlayerController _p;
        private Vector3 _destination;
        private bool _isActive;

        public PlayerAutoMoveState(PlayerController player) => _p = player;

        public void SetDestination(Vector3 destination)
        {
            _destination = destination;
            if (_isActive) _p.Agent.SetDestination(_destination);
            _p.NotifyDestinationSet(_destination);
        }

        public void Enter()
        {
            _isActive = true;
            var agent = _p.Agent;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(_destination);
        }

        public void Tick(float deltaTime)
        {
            if (_p.HasMoveInput)
            {
                _p.StateMachine.ChangeState(_p.ManualMoveState);
                return;
            }

            var agent = _p.Agent;
            if (agent.pathPending) return;

            bool invalid = agent.pathStatus == NavMeshPathStatus.PathInvalid;
            bool arrived = agent.remainingDistance <= agent.stoppingDistance + _p.ArriveThreshold;
            if (invalid || arrived)
                _p.StateMachine.ChangeState(_p.IdleState);
        }

        public void Exit()
        {
            _isActive = false;
            if (_p.Agent.isOnNavMesh) _p.Agent.ResetPath();
            _p.NotifyDestinationCleared();
        }
    }
}
