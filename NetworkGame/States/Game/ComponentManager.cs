using System.Numerics;

public class ComponentManager
{
    private Dictionary<Type, IComponentStorage> _storages = new();

    public void AddComponent<T>(T component, Vector2 position) where T : TileComponent
    {
        var type = typeof(T);

        if (!_storages.ContainsKey(type))
        {
            _storages.Add(type, new ComponentStorage<T>());
        }

        ((ComponentStorage<T>)_storages[type]).Add(component, position);
    }

    public T? Query<T>(Vector2 position) where T : TileComponent
    {
        var type = typeof(T);

        if (!_storages.ContainsKey(typeof(T)))
        {
            return null;
        }

        T? tileComponent = ((ComponentStorage<T>)_storages[type]).Get(position);

        if (tileComponent != null) return tileComponent;
        return null;
    }

    public IEnumerable<(Vector2 position, T component)> QueryAll<T>() where T : TileComponent
    {
        if (!_storages.TryGetValue(typeof(T), out var storage))
            yield break;
    
        var typed = (ComponentStorage<T>)storage;
    
        var all = typed.GetAll();

        foreach (var e in all)
        {
            yield return (e.position, e.component);
        }
    }

    public void RemoveAll(Vector2 position)
    {
        foreach (IComponentStorage storage in _storages.Values)
        {
            storage.Remove(position);
        }
    }
}