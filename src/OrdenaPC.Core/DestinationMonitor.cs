namespace OrdenaPC.Core;

/// <summary>Registra desde cuándo cada carpeta destino está sin acceso (por ejemplo, Google Drive cerrado).</summary>
public sealed class DestinationMonitor
{
    private readonly Dictionary<string, DateTime> _downSince = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Devuelve los destinos sin acceso y desde cuándo (UTC).</summary>
    public IReadOnlyList<KeyValuePair<string, DateTime>> Check(
        IEnumerable<string> destinations, DateTime nowUtc, Func<string, bool>? isAvailable = null)
    {
        isAvailable ??= SafeMover.IsAvailable;
        var current = destinations
            .Select(TextUtil.TryNormalizeFolder)
            .Where(d => d.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var gone in _downSince.Keys.Except(current, StringComparer.OrdinalIgnoreCase).ToList())
            _downSince.Remove(gone);

        foreach (var dest in current)
        {
            if (isAvailable(dest)) _downSince.Remove(dest);
            else if (!_downSince.ContainsKey(dest)) _downSince[dest] = nowUtc;
        }
        return _downSince.ToList();
    }
}
