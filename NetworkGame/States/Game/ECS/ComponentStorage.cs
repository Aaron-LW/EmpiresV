public class ComponentStorage<T> : IComponentStorage where T : Component
{
    public Dictionary<int, T> _components = new();

    public void Add(int id, Component component)
    {
        Add(id, (T)component);
    }

    private void Add(int id, T component)
    {
        _components.Add(id, component);
    }

    public void Remove(int id)
    {
        _components.Remove(id);
    }

    public T? Get(int id)
    {
        if (_components.TryGetValue(id, out T? component))
        {
            return component;
        }

        return null;
    }

    public IEnumerable<(int id, T component)> GetAll()
    {
        foreach (KeyValuePair<int, T> keyValuePair in _components)
        {
            yield return (keyValuePair.Key, keyValuePair.Value);
        }
    }
}