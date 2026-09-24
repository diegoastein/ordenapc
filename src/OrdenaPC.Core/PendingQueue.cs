namespace OrdenaPC.Core;

/// <summary>Archivos que no se pudieron mover porque el destino no estaba disponible.</summary>
public sealed class PendingQueue
{
    private readonly HashSet<string> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public bool Add(string path) { lock (_lock) return _items.Add(path); }
    public bool Remove(string path) { lock (_lock) return _items.Remove(path); }
    public int Count { get { lock (_lock) return _items.Count; } }
    public IReadOnlyList<string> Snapshot() { lock (_lock) return _items.ToList(); }
}
