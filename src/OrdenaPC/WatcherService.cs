using System.Collections.Concurrent;
using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>
/// Vigilancia en tiempo real (FileSystemWatcher) más un barrido periódico de respaldo,
/// por si el watcher pierde eventos, y un reintento de pendientes cada 5 minutos.
/// </summary>
sealed class WatcherService : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    private readonly Organizer _organizer;
    private readonly Func<AppConfig> _config;
    private readonly List<FileSystemWatcher> _watchers = new();
    // Archivo -> momento (UTC) a partir del cual se procesa.
    private readonly ConcurrentDictionary<string, DateTime> _queue = new(StringComparer.OrdinalIgnoreCase);
    private System.Threading.Timer? _flushTimer, _sweepTimer, _retryTimer;
    private int _flushing, _sweeping;

    public WatcherService(Organizer organizer, Func<AppConfig> config)
    {
        _organizer = organizer;
        _config = config;
    }

    /// <summary>Se dispara desde un hilo del pool; el bool indica si vino de un barrido completo.</summary>
    public event Action<IReadOnlyList<MoveResult>, bool>? Processed;

    public void Start()
    {
        Stop();
        var config = _config();
        var folders = config.Reglas
            .Where(r => r.Activa)
            .Select(r => TextUtil.TryNormalizeFolder(r.CarpetaOrigen))
            .Where(f => f.Length > 0 && Directory.Exists(f))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            try
            {
                var w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024,
                };
                w.Created += (_, e) => Enqueue(e.FullPath);
                w.Changed += (_, e) => Enqueue(e.FullPath);
                w.Renamed += (_, e) => Enqueue(e.FullPath);
                w.Error += (_, _) => ThreadPool.QueueUserWorkItem(_ => Sweep());
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch (Exception)
            {
                // Si una carpeta no se puede vigilar, el barrido periódico la sigue cubriendo.
            }
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, config.IntervaloBarridoMin));
        _flushTimer = new(_ => Flush(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        // Un barrido poco después de arrancar agarra lo que llegó mientras la app estaba cerrada.
        _sweepTimer = new(_ => Sweep(), null, TimeSpan.FromSeconds(10), interval);
        _retryTimer = new(_ => Retry(), null, RetryInterval, RetryInterval);
    }

    public void Stop()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        _flushTimer?.Dispose();
        _sweepTimer?.Dispose();
        _retryTimer?.Dispose();
        _flushTimer = _sweepTimer = _retryTimer = null;
        _queue.Clear();
    }

    public void Dispose() => Stop();

    // Cada evento nuevo corre el momento: así se espera a que el archivo deje de recibir eventos.
    private void Enqueue(string path) => _queue[path] = DateTime.UtcNow + Debounce;

    /// <summary>Agenda un archivo para procesarlo en un momento dado (lo usa la espera antes de mover).</summary>
    public void Schedule(string path, DateTime dueUtc) => _queue[path] = dueUtc;

    private void Flush()
    {
        if (Interlocked.Exchange(ref _flushing, 1) == 1) return;
        try
        {
            var now = DateTime.UtcNow;
            var results = new List<MoveResult>();
            foreach (var entry in _queue)
            {
                if (entry.Value > now) continue;
                // Solo se saca si no llegó un evento nuevo mientras tanto (compara también la fecha).
                if (!((ICollection<KeyValuePair<string, DateTime>>)_queue).Remove(entry)) continue;
                var r = _organizer.ProcessFile(entry.Key);
                if (r != null) results.Add(r);
            }
            if (results.Count > 0) Processed?.Invoke(results, false);
        }
        catch (Exception) { }
        finally { Volatile.Write(ref _flushing, 0); }
    }

    private void Sweep()
    {
        if (Interlocked.Exchange(ref _sweeping, 1) == 1) return;
        try
        {
            Processed?.Invoke(_organizer.ScanAll(simulate: false), true);
        }
        catch (Exception) { }
        finally { Volatile.Write(ref _sweeping, 0); }
    }

    private void Retry()
    {
        try
        {
            var results = _organizer.RetryPending();
            if (results.Count > 0) Processed?.Invoke(results, false);
        }
        catch (Exception) { }
    }
}
