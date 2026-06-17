using Smash.Graphics;

public abstract class GameState : IState
{
    public GameState(StateResult previousStateResult) { }

    public virtual void Enter() { }
    public virtual void Update(double deltaTime) { }
    public virtual void Render(Renderer renderer) { }
    protected virtual void OnStateFinish<T>(T stateResult) where T : StateResult { StateFinish?.Invoke(this, new StateFinishEventArgs { StateResult = stateResult }); }
    public event EventHandler? StateFinish;
    public virtual void ForwardPacket(byte[] data) { }
}