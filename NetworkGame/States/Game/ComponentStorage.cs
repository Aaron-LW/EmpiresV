using System.Numerics;

public class ComponentStorage<T> : IComponentStorage where T : TileComponent
{
    private Dictionary<Vector2, T> _components = new();

    public T? Get(Vector2 position)
    {
        if (_components.TryGetValue(position, out T? component))
        {
            return component;
        }

        return null;
    }

    public IEnumerable<(Vector2 position, T component)> GetAll()
    {
        foreach (KeyValuePair<Vector2, T> keyValuePair in _components)
        {
            yield return (keyValuePair.Key, keyValuePair.Value);
        }
    }

    public void Add(TileComponent component, Vector2 position)
    {
        Add((T)component, position);
    }

    private void Add(T component, Vector2 position)
    {
        _components.Add(position, component);
    }

    public void Remove(Vector2 id)
    {
        _components.Remove(id);
    }
}