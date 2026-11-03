using System;

namespace RPGAuto.Core
{
    public class StateMachine
    {
        public IState Current { get; private set; }

        /// <summary>상태 변경 알림 (Observer) — 애니메이션/UI/디버그 연동용</summary>
        public event Action<IState> OnStateChanged;

        public void ChangeState(IState next)
        {
            if (next == null || next == Current) return;

            Current?.Exit();
            Current = next;
            Current.Enter();
            OnStateChanged?.Invoke(Current);
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
