using Smash.Graphics;

public interface IState
{
    public void Enter();
    public void Update(double deltaTime);
    public void Render(Renderer renderer);
    public StateResult Exit();
}