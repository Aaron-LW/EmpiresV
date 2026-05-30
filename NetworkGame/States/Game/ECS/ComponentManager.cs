public class ComponentManager
{
    private Dictionary<Type, IComponentStorage> _storages = new();

    public void AddComponent<T>(int id, T component) where T : Component
    {
        var type = typeof(T);

        if (!_storages.ContainsKey(type))
        {
            _storages.Add(type, new ComponentStorage<T>());
        }

        ((ComponentStorage<T>)_storages[type]).Add(id, component);
    }

    public T? Query<T>(int id) where T : Component
    {
        var type = typeof(T);

        if (!_storages.ContainsKey(typeof(T)))
        {
            return null;
        }

        T? tileComponent = ((ComponentStorage<T>)_storages[type]).Get(id);

        if (tileComponent != null) return tileComponent;
        return null;
    }

    public IEnumerable<(int id, T component)> QueryAll<T>() where T : Component
    {
        if (!_storages.TryGetValue(typeof(T), out var storage))
            yield break;
    
        var typed = (ComponentStorage<T>)storage;
    
        var all = typed.GetAll();

        foreach (var e in all)
        {
            yield return (e.id, e.component);
        }
    }

    public void RemoveAll(int id)
    {
        foreach (IComponentStorage storage in _storages.Values)
        {
            storage.Remove(id);
        }
    }
}