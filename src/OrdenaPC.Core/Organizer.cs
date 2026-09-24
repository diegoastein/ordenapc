namespace OrdenaPC.Core;

/// <summary>
/// Aplica las reglas a los archivos. Lo usan el watcher, el barrido periódico, la
/// simulación y el primer barrido. Las operaciones se serializan con un lock porque
/// llegan desde varios hilos (eventos del watcher y timers).
/// </summary>
public sealed class Organizer
{
    private readonly Func<AppConfig> _config;
    private readonly SafeMover _mover;
    private readonly MoveLog? _log;
    private readonly object _lock = new();
    private readonly HashSet<string> _reportedErrors = new(StringComparer.OrdinalIgnoreCase);

    public Organizer(Func<AppConfig> config, SafeMover mover, MoveLog? log)
    {
        _config = config;
        _mover = mover;
        _log = log;
    }

    public PendingQueue Pending { get; } = new();

    private List<Rule> ActiveRules() => _config().Reglas.Where(r => r.Activa).ToList();

    /// <summary>Recorre las carpetas origen. Con onlyRule se evalúa solo esa regla (aunque esté inactiva).</summary>
    public IReadOnlyList<MoveResult> ScanAll(bool simulate, Rule? onlyRule = null)
    {
        var rules = onlyRule != null ? new List<Rule> { onlyRule } : ActiveRules();
        var results = new List<MoveResult>();
        var folders = rules
            .Select(r => TextUtil.TryNormalizeFolder(r.CarpetaOrigen))
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch (Exception) { continue; }

            foreach (var file in files)
            {
                var r = ProcessFile(file, simulate, rules);
                if (r != null) results.Add(r);
            }
        }
        return results;
    }

    /// <summary>Devuelve null si el archivo no corresponde a ninguna regla (y entonces no se toca).</summary>
    public MoveResult? ProcessFile(string path, bool simulate = false, IReadOnlyList<Rule>? rules = null)
    {
        lock (_lock)
        {
            rules ??= ActiveRules();
            if (!File.Exists(path) || FileFilter.ShouldIgnore(path))
            {
                Pending.Remove(path);
                return null;
            }
            if (!simulate && IsExcluded(path)) return null;

            var rule = RuleMatcher.FindRule(rules, path);
            if (rule == null)
            {
                Pending.Remove(path);
                return null;
            }

            var result = _mover.Move(path, rule.CarpetaDestino, rule.NombreVisible, simulate);
            if (simulate) return result;

            // Pendientes y errores se registran una sola vez, no en cada reintento.
            switch (result.Estado)
            {
                case MoveStatus.Movido:
                    Pending.Remove(path);
                    _reportedErrors.Remove(path);
                    _log?.Append(result);
                    break;
                case MoveStatus.Pendiente:
                    if (Pending.Add(path)) _log?.Append(result);
                    break;
                case MoveStatus.Error:
                    if (_reportedErrors.Add(path)) _log?.Append(result);
                    break;
            }
            return result;
        }
    }

    public IReadOnlyList<MoveResult> RetryPending() =>
        Pending.Snapshot().Select(p => ProcessFile(p)).OfType<MoveResult>().ToList();

    /// <summary>
    /// Devuelve un archivo movido a su carpeta original y lo agrega a Excluidos para que
    /// no se vuelva a mover solo. Quien llama tiene que guardar la configuración.
    /// </summary>
    public MoveResult Undo(MoveResult entry)
    {
        lock (_lock)
        {
            if (entry.Estado != MoveStatus.Movido)
                return new(DateTime.Now, "Deshacer", entry.Destino, entry.Origen, MoveStatus.Error,
                    "Solo se pueden deshacer movimientos realizados.");
            if (!File.Exists(entry.Destino))
                return new(DateTime.Now, "Deshacer", entry.Destino, entry.Origen, MoveStatus.Error,
                    "El archivo ya no está en el destino.");

            var originDir = Path.GetDirectoryName(entry.Origen)!;
            var r = _mover.Move(entry.Destino, originDir, "Deshacer", simulate: false,
                targetName: Path.GetFileName(entry.Origen));

            if (r.Estado == MoveStatus.Movido)
            {
                r = r with { Estado = MoveStatus.Deshecho };
                var excluded = _config().Excluidos;
                if (!excluded.Contains(r.Destino, StringComparer.OrdinalIgnoreCase)) excluded.Add(r.Destino);
            }
            _log?.Append(r);
            return r;
        }
    }

    private bool IsExcluded(string path)
    {
        var full = Path.GetFullPath(path);
        return _config().Excluidos.Contains(full, StringComparer.OrdinalIgnoreCase);
    }
}
