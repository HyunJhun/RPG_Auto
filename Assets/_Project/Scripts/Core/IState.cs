namespace RPGAuto.Core
{
    /// <summary>State Pattern 공용 상태 인터페이스 (플레이어/몬스터 공용)</summary>
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
