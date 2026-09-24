using System.Globalization;

namespace OrdenaPC.Core;

/// <summary>
/// Aplica las reglas, las limpiezas y los buzones. Lo usan el watcher, el barrido periódico,
/// la simulación y el primer barrido. Las operaciones se serializan con un lock porque
/// llegan desde varios hilos (eventos del watcher, timers y buzones).
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

    /// <summary>Donde quedan los archivos soltados en un buzón mientras su destino no está disponible.</summary>
    public string StagingRoot { get; set; } = Path.Combine(Path.GetTempPath(), "OrdenaPC-buzones");

    /// <summary>A dónde vuelve un archivo al deshacer si su origen era la carpeta interna de un buzón.</summary>
    public string? UndoFallbackDir { get; set; }

    private List<Rule> ActiveRules() => _config().Reglas.Where(r => r.Activa).ToList();

    /// <summary>
    /// Recorre las carpetas origen aplicando reglas y limpiezas. Con onlyRule u onlyCleanup se
    /// evalúa solo esa (aunque esté inactiva). Un barrido completo también reintenta los buzones.
    /// </summary>
    public IReadOnlyList<MoveResult> ScanAll(bool simulate, Rule? onlyRule = null, CleanupRule? onlyCleanup = null)
    {
        List<Rule> rules;
        List<CleanupRule> cleanups;
        if (onlyRule != null)
        {
            rules = new List<Rule> { onlyRule };
            cleanups = new List<CleanupRule>();
        }
        else if (onlyCleanup != null)
        {
            rules = ActiveRules();
            cleanups = new List<CleanupRule> { onlyCleanup };
        }
        else
        {
            rules = ActiveRules();
            cleanups = _config().Limpiezas.Where(c => c.Activa).ToList();
        }

        var folders = (onlyCleanup != null ? Enumerable.Empty<string>() : rules.Select(r => r.CarpetaOrigen))
            .Concat(cleanups.Select(c => c.CarpetaOrigen))
            .Select(TextUtil.TryNormalizeFolder)
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var results = new List<MoveResult>();
        foreach (var folder in folders)
        {
            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch (Exception) { continue; }

            foreach (var file in files)
            {
                var r = onlyCleanup == null ? ProcessFile(file, simulate, rules) : null;
                r ??= ProcessCleanupFile(file, cleanups, rules, simulate);
                if (r != null) results.Add(r);
            }
        }

        if (!simulate && onlyRule == null && onlyCleanup == null)
            results.AddRange(RetryMailboxes());
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

            var readyAt = ReadyAtUtc(path);
            if (readyAt != null)
            {
                if (simulate)
                {
                    var sim = _mover.Move(path, rule.CarpetaDestino, rule.NombreVisible, simulate: true);
                    if (sim.Estado != MoveStatus.Simulado) return sim;
                    var note = $"Se moverá cuando pasen {_config().EsperaMinutos} min sin cambios.";
                    return sim with { Detalle = sim.Detalle.Length > 0 ? sim.Detalle + " " + note : note };
                }
                return new MoveResult(DateTime.Now, rule.NombreVisible, path, "", MoveStatus.Esperando,
                    $"Se mueve a las {readyAt.Value.ToLocalTime():HH:mm}.");
            }

            var result = _mover.Move(path, rule.CarpetaDestino, rule.NombreVisible, simulate);
            if (!simulate) Record(path, result);
            return result;
        }
    }

    /// <summary>Limpieza: archivos sin regla que llevan más de N días sin cambios.</summary>
    private MoveResult? ProcessCleanupFile(string path, IReadOnlyList<CleanupRule> cleanups, IReadOnlyList<Rule> rules,
        bool simulate)
    {
        if (cleanups.Count == 0) return null;
        lock (_lock)
        {
            if (!File.Exists(path) || FileFilter.ShouldIgnore(path) || IsExcluded(path)) return null;
            if (RuleMatcher.FindRule(rules, path) != null) return null; // de ese archivo se encarga su regla

            var cleanup = cleanups.FirstOrDefault(c => RuleMatcher.IsInFolder(c.CarpetaOrigen, path));
            var last = LastChangeUtc(path);
            if (cleanup == null || last == null) return null;
            if (DateTime.UtcNow - last.Value < TimeSpan.FromDays(Math.Max(1, cleanup.Dias))) return null;

            var baseDest = TextUtil.TryNormalizeFolder(cleanup.CarpetaDestino);
            if (baseDest.Length == 0) return null;
            var dest = cleanup.SubcarpetaPorMes
                ? Path.Combine(baseDest, last.Value.ToLocalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture))
                : baseDest;

            if (simulate)
            {
                // La carpeta base todavía no existe pero se puede crear: la simulación no es "no disponible".
                if (!Directory.Exists(baseDest) && SafeMover.IsAvailable(baseDest))
                    return new MoveResult(DateTime.Now, cleanup.NombreRegla, path, Path.Combine(dest, Path.GetFileName(path)),
                        MoveStatus.Simulado, "Se crea la carpeta destino.");
                return _mover.Move(path, dest, cleanup.NombreRegla, simulate: true);
            }

            SafeMover.TryEnsureFolder(baseDest);
            var result = _mover.Move(path, dest, cleanup.NombreRegla, simulate: false);
            Record(path, result);
            return result;
        }
    }

    // Pendientes y errores se registran una sola vez, no en cada reintento.
    private void Record(string path, MoveResult result)
    {
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
    }

    public string StagingDir(Mailbox box) => Path.Combine(StagingRoot, box.Id.ToString("N"));

    /// <summary>
    /// Archivos soltados en un buzón. Si el destino no está disponible, se guardan en la carpeta
    /// interna del buzón y RetryMailboxes los envía después. Desde un pendrive o red se copian.
    /// </summary>
    public IReadOnlyList<MoveResult> DropIntoMailbox(Mailbox box, IEnumerable<string> paths)
    {
        var results = new List<MoveResult>();
        lock (_lock)
        {
            var dest = TextUtil.TryNormalizeFolder(box.CarpetaDestino);
            foreach (var raw in paths)
            {
                string path;
                try { path = Path.GetFullPath(raw); }
                catch (Exception) { continue; }

                if (Directory.Exists(path))
                {
                    results.Add(new MoveResult(DateTime.Now, box.NombreRegla, path, "", MoveStatus.Error,
                        "Las carpetas no se pueden archivar: arrastrá los archivos de adentro."));
                    continue;
                }
                if (!File.Exists(path)) continue;
                if (dest.Length > 0 && RuleMatcher.IsInFolder(dest, path)) continue; // ya está en el destino

                bool keepSource = !IsOnLocalFixedDrive(path);
                var r = _mover.Move(path, box.CarpetaDestino, box.NombreRegla, simulate: false, keepSource: keepSource);
                if (r.Estado == MoveStatus.Pendiente)
                {
                    var staging = StagingDir(box);
                    Directory.CreateDirectory(staging);
                    var staged = _mover.Move(path, staging, box.NombreRegla, simulate: false, keepSource: keepSource);
                    r = staged.Estado == MoveStatus.Movido
                        ? r with { Detalle = "Guardado en la PC; se envía a la carpeta destino cuando esté disponible." }
                        : staged;
                }
                if (r.Estado is MoveStatus.Movido or MoveStatus.Pendiente or MoveStatus.Error) _log?.Append(r);
                results.Add(r);
            }
        }
        return results;
    }

    /// <summary>Envía a su destino los archivos que quedaron guardados en los buzones.</summary>
    public IReadOnlyList<MoveResult> RetryMailboxes()
    {
        var results = new List<MoveResult>();
        lock (_lock)
        {
            foreach (var box in _config().Buzones)
            {
                foreach (var file in StagedFiles(box))
                {
                    var r = _mover.Move(file, box.CarpetaDestino, box.NombreRegla, simulate: false);
                    if (r.Estado == MoveStatus.Movido)
                    {
                        _reportedErrors.Remove(file);
                        _log?.Append(r);
                        results.Add(r);
                    }
                    else if (r.Estado == MoveStatus.Error && _reportedErrors.Add(file))
                    {
                        _log?.Append(r);
                    }
                }
            }
        }
        return results;
    }

    public int MailboxPendingCount() => _config().Buzones.Sum(b => StagedFiles(b).Count);

    public IReadOnlyList<string> StagedFiles(Mailbox box)
    {
        try
        {
            var dir = StagingDir(box);
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir)
                .Where(f => !f.EndsWith(".ordenapc-tmp", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<MoveResult> RetryPending() =>
        Pending.Snapshot().Select(p => ProcessFile(p)).OfType<MoveResult>().Concat(RetryMailboxes()).ToList();

    /// <summary>
    /// Si el archivo se creó o modificó hace menos de EsperaMinutos, devuelve cuándo se puede mover (UTC).
    /// Si ya se puede mover, o no hay espera configurada, devuelve null.
    /// </summary>
    public DateTime? ReadyAtUtc(string path)
    {
        var wait = _config().EsperaMinutos;
        if (wait <= 0) return null;
        var last = LastChangeUtc(path);
        if (last == null) return null;
        var ready = last.Value.AddMinutes(wait);
        return ready > DateTime.UtcNow ? ready : null;
    }

    // Un archivo copiado conserva la fecha de modificación original, pero la de creación es nueva.
    private static DateTime? LastChangeUtc(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.LastWriteTimeUtc > info.CreationTimeUtc ? info.LastWriteTimeUtc : info.CreationTimeUtc;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>El movimiento más reciente cuyo archivo sigue en el destino (el que se puede deshacer).</summary>
    public static MoveResult? LastUndoable(IReadOnlyList<MoveResult> log)
    {
        for (int i = log.Count - 1; i >= 0; i--)
        {
            if (log[i].Estado == MoveStatus.Movido && File.Exists(log[i].Destino)) return log[i];
        }
        return null;
    }

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
            // Si salió de la carpeta interna de un buzón, volver ahí haría que se reenvíe: va al Escritorio.
            if (UndoFallbackDir != null && IsUnder(originDir, StagingRoot)) originDir = UndoFallbackDir;

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

    private static bool IsUnder(string path, string root)
    {
        var p = TextUtil.TryNormalizeFolder(path);
        var r = TextUtil.TryNormalizeFolder(root);
        return p.Length > 0 && r.Length > 0 &&
               (p.Equals(r, StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOnLocalFixedDrive(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root) || root!.StartsWith(@"\\")) return false;
            return new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
