using System.Numerics;

public interface IComponentStorage
{
    void Add(TileComponent component, Vector2 id);
    void Remove(Vector2 id);
}