public class CoolnessSystem : ITileSystem
{
    public void Update(ComponentManager componentManager, double deltaTime)
    {
        var coolComponents = componentManager.QueryAll<CoolComponent>();

        foreach (var coolComponent in coolComponents)
        {
            Console.WriteLine(coolComponent.position);
        }
    }
}