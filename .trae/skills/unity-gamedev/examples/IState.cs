namespace Game.Architecture.FSM
{
    /// <summary>
    /// Base interface for all states in a Finite State Machine.
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void Tick();
        void OnExit();
    }
}
