public class Entity
{
    private static int _nextId = 0;

    public readonly int Id;

    public Entity()
    {
        Id = _nextId++;
    }
}