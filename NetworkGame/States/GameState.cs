using Smash.Graphics;

public abstract class GameState : IState
{
    protected StateMachine _stateMachine;
    protected Window _window;

    public GameState(StateMachine stateMachine, Window window)
    {
        _stateMachine = stateMachine; 
        _window = window;
    }

    public virtual void Enter() { }
    public virtual void Update(double deltaTime) { }
    public virtual void Render(Renderer renderer) { }
    public abstract StateResult Exit();
}