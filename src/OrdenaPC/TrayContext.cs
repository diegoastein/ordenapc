using System.Diagnostics;
using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>Contexto de la app: ícono en la bandeja, configuración, watcher, buzones y ventanas.</summary>
sealed class TrayContext : ApplicationContext
{
    public static string DefaultConfigPath => Path.Combine(AppConfig.DataDir, "config.json");

    private static readonly TimeSpan PinGrace = TimeSpan.FromMinutes(5);

    private readonly WatcherService _watcher;
    private readonly MailboxManager _mailboxes;
    private readonly DestinationMonitor _monitor = new();
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _undoItem;
    private readonly System.Windows.Forms.Timer _healthTimer = new() { Interval = 5 * 60 * 1000 };
    private readonly Control _ui = new();
    private readonly Icon _iconOn = TrayIcons.Make(Color.FromArgb(0, 120, 212));
    private readonly Icon _iconOff = TrayIcons.Make(Color.Gray);
    private MainForm? _main;
    private LogForm? _logForm;
    private AlertForm? _alert;
    private MoveResult? _undoable;
    private DateTime _lastPendingNotice = DateTime.MinValue;
    private DateTime _lastAlertUtc = DateTime.MinValue;
    private DateTime _unlockedUntilUtc = DateTime.MinValue;

    /// <param name="userLaunch">false cuando la abrió la tarea de reapertura (--background).</param>
    public TrayContext(bool userLaunch)
    {
        _ = _ui.Handle; // crea el handle para poder volver al hilo de la UI desde los timers

        Config = AppConfig.Load(ConfigPath);
        Log = new MoveLog(Path.Combine(AppConfig.DataDir, "log.csv"));
        Organizer = new Organizer(() => Config, new SafeMover(), Log)
        {
            StagingRoot = Path.Combine(Installer.InstallDir, "Buzones"),
            UndoFallbackDir = KnownFolders.Desktop,
        };
        _watcher = new WatcherService(Organizer, () => Config);
        _watcher.Processed += (results, sweep) => _ui.BeginInvoke((Action)(() => OnProcessed(results, sweep)));
        _mailboxes = new MailboxManager(this);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir panel", null, (_, _) => ShowMain());
        menu.Items.Add("Ejecutar ahora", null, async (_, _) => await RunNowAsync(null));
        _pauseItem = new ToolStripMenuItem("Pausar", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        _undoItem = new ToolStripMenuItem("Deshacer último movimiento", null, (_, _) => UndoLast());
        menu.Items.Add(_undoItem);
        menu.Items.Add("Mostrar buzones", null, (_, _) => _mailboxes.ShowAll());
        menu.Items.Add("Ver log", null, (_, _) => ShowLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApp());
        menu.Opening += (_, _) => RefreshUndoItem();

        _tray = new NotifyIcon { ContextMenuStrip = menu, Icon = _iconOff, Text = "OrdenaPC", Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        _healthTimer.Tick += (_, _) => CheckHealth();
        _healthTimer.Start();

        // Abrirla a mano (o al iniciar Windows) anula un "Salir" anterior.
        if (userLaunch && Config.Detenido)
        {
            Config.Detenido = false;
            TrySave();
        }
        Watchdog.Apply(!Config.NoReabrir);
        if (Autostart.IsEnabled()) Autostart.Set(true); // actualiza la ruta y los argumentos

        ApplyState();
        // Sin PIN (recién instalada) se abre el panel para configurar; con PIN no se molesta al que usa la PC.
        if (!Config.TienePin && ((Config.Reglas.Count == 0 && Config.Buzones.Count == 0) || NeedsFirstSweep))
            _ui.BeginInvoke((Action)(() => ShowMain()));
    }

    public AppConfig Config { get; }
    public string ConfigPath { get; } = DefaultConfigPath;
    public MoveLog Log { get; }
    public Organizer Organizer { get; }
    public MailboxManager Mailboxes => _mailboxes;

    /// <summary>Cambió el estado (pausa, pendientes, primer barrido); las ventanas se refrescan.</summary>
    public event Action? StateChanged;

    private bool HasAutomaticWork =>
        Config.Reglas.Any(r => r.Activa) || Config.Limpiezas.Any(c => c.Activa) || Config.Buzones.Any(b => b.Activo);

    /// <summary>Hay reglas o limpiezas activas y todavía no se revisó qué movería el primer barrido.</summary>
    public bool NeedsFirstSweep =>
        !Config.PrimerBarridoHecho && (Config.Reglas.Any(r => r.Activa) || Config.Limpiezas.Any(c => c.Activa));

    public bool IsActive => !Config.Pausado && !NeedsFirstSweep && HasAutomaticWork;

    public string StatusText
    {
        get
        {
            if (Config.Pausado) return "En pausa";
            if (!HasAutomaticWork) return "Sin reglas, buzones ni limpiezas activas";
            if (NeedsFirstSweep) return "En espera: falta revisar el primer barrido";
            int pending = Organizer.Pending.Count + Organizer.MailboxPendingCount();
            return pending > 0 ? $"Vigilando · {pending} esperando a que el destino esté disponible" : "Vigilando";
        }
    }

    // ---------- PIN ----------

    /// <summary>Pide el PIN (si hay uno) antes de una acción protegida. Vale 5 minutos.</summary>
    public bool RequirePin(IWin32Window? owner, string action)
    {
        if (!Config.TienePin || DateTime.UtcNow < _unlockedUntilUtc) return true;
        using var dialog = new PinDialog(action, pin => PinHasher.Verify(pin, Config.PinHash, Config.PinSalt));
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        _unlockedUntilUtc = DateTime.UtcNow + PinGrace;
        return true;
    }

    /// <summary>Mientras el panel está abierto no se vuelve a pedir el PIN.</summary>
    public void KeepUnlocked()
    {
        if (Config.TienePin) _unlockedUntilUtc = DateTime.UtcNow + PinGrace;
    }

    // ---------- Configuración y estado ----------

    /// <summary>Guarda la configuración y reinicia la vigilancia con los cambios.</summary>
    public void SaveConfig()
    {
        if (!TrySave())
            MessageBox.Show("No se pudo guardar la configuración.", "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        ApplyState();
    }

    private bool TrySave()
    {
        try
        {
            Config.Save(ConfigPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ApplyState()
    {
        _watcher.Stop();
        if (IsActive) _watcher.Start();
        _pauseItem.Text = Config.Pausado ? "Reanudar" : "Pausar";
        _mailboxes.Reload();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        _tray.Icon = IsActive ? _iconOn : _iconOff;
        var text = "OrdenaPC · " + StatusText;
        _tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        StateChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (!RequirePin(null, Config.Pausado ? "reanudar OrdenaPC" : "pausar OrdenaPC")) return;
        Config.Pausado = !Config.Pausado;
        SaveConfig();
    }

    // ---------- Barridos ----------

    public async Task RunNowAsync(IWin32Window? owner)
    {
        if (NeedsFirstSweep)
        {
            ShowMain();
            if (_main != null) await ReviewFirstSweepAsync(_main);
            return;
        }
        var results = await Task.Run(() => Organizer.ScanAll(simulate: false));
        OnProcessed(results, sweep: true);
        ShowSummary(owner, results);
    }

    /// <summary>Simula el barrido completo, muestra qué movería y recién con la confirmación mueve y activa la vigilancia.</summary>
    public async Task ReviewFirstSweepAsync(IWin32Window? owner)
    {
        var simulated = await Task.Run(() => Organizer.ScanAll(simulate: true));
        using (var form = new SimulationForm(simulated,
                   "Primer barrido: esto es lo que se movería",
                   simulated.Any(r => r.Estado == MoveStatus.Simulado) ? "Mover estos archivos y activar" : "Activar vigilancia",
                   "Cancelar (revisar reglas)"))
        {
            if (form.ShowDialog(owner) != DialogResult.OK) return;
        }

        var results = await Task.Run(() => Organizer.ScanAll(simulate: false));
        Config.PrimerBarridoHecho = true;
        SaveConfig();
        OnProcessed(results, sweep: true);
        if (results.Count > 0) ShowSummary(owner, results);
    }

    public void ActivateWithoutReview(IWin32Window? owner)
    {
        if (!FormKit.Confirm(owner,
                "La vigilancia se activa ya, sin simulación previa. El primer barrido va a mover todos los archivos " +
                "existentes que coincidan con las reglas y limpiezas.\n\n¿Continuar?"))
            return;
        Config.PrimerBarridoHecho = true;
        SaveConfig();
    }

    private void OnProcessed(IReadOnlyList<MoveResult> results, bool sweep)
    {
        // Los recién modificados se agendan para cuando se cumpla la espera, sin depender del barrido periódico.
        foreach (var r in results.Where(r => r.Estado == MoveStatus.Esperando))
        {
            var readyAt = Organizer.ReadyAtUtc(r.Origen);
            if (readyAt != null) _watcher.Schedule(r.Origen, readyAt.Value.AddSeconds(1));
        }

        var moved = results.Where(r => r.Estado == MoveStatus.Movido).ToList();
        var pending = results.Count(r => r.Estado == MoveStatus.Pendiente);

        if (Config.Notificaciones == NotificationMode.CadaArchivo && moved.Count > 0)
        {
            if (moved.Count == 1)
                Balloon("Archivo ordenado",
                    $"{Path.GetFileName(moved[0].Destino)} → {Path.GetFileName(Path.GetDirectoryName(moved[0].Destino))}");
            else
                Balloon("Archivos ordenados", $"{moved.Count} archivos ordenados");
        }
        else if (Config.Notificaciones == NotificationMode.Resumen && sweep && moved.Count > 0)
        {
            Balloon("Barrido terminado", moved.Count == 1 ? "1 archivo ordenado" : $"{moved.Count} archivos ordenados");
        }

        if (pending > 0 && Config.Notificaciones != NotificationMode.Nunca &&
            DateTime.Now - _lastPendingNotice > TimeSpan.FromHours(1))
        {
            _lastPendingNotice = DateTime.Now;
            Balloon("Destino no disponible",
                $"{pending} archivo(s) esperan a que el destino (¿Google Drive?) esté disponible. Se reintenta solo.",
                ToolTipIcon.Warning);
        }

        if (moved.Count > 0 || pending > 0)
        {
            _logForm?.Reload();
            _mailboxes.RefreshCounts();
        }
        RefreshStatus();
    }

    /// <summary>Después de soltar archivos en un buzón (el buzón ya muestra su propio mensaje).</summary>
    public void OnMailboxDrop(IReadOnlyList<MoveResult> results)
    {
        _logForm?.Reload();
        _mailboxes.RefreshCounts();
        RefreshStatus();
    }

    private void Balloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        _tray.ShowBalloonTip(5000, title, text, icon);

    public static void ShowSummary(IWin32Window? owner, IReadOnlyList<MoveResult> results)
    {
        int moved = results.Count(r => r.Estado == MoveStatus.Movido);
        int pending = results.Count(r => r.Estado == MoveStatus.Pendiente);
        int inUse = results.Count(r => r.Estado == MoveStatus.EnUso);
        int errors = results.Count(r => r.Estado == MoveStatus.Error);
        int waiting = results.Count(r => r.Estado == MoveStatus.Esperando);

        var lines = new List<string> { moved == 1 ? "1 archivo ordenado." : $"{moved} archivos ordenados." };
        if (pending > 0) lines.Add($"{pending} esperando a que el destino esté disponible (se reintenta solo).");
        if (waiting > 0) lines.Add($"{waiting} modificados hace poco: se mueven solos cuando se cumpla el tiempo de espera.");
        if (inUse > 0) lines.Add($"{inUse} abiertos en otro programa (se reintenta en el próximo barrido).");
        if (errors > 0) lines.Add($"{errors} con error (ver el log).");
        MessageBox.Show(owner, string.Join("\n", lines), "OrdenaPC", MessageBoxButtons.OK,
            errors > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    // ---------- Alerta de destino sin acceso ----------

    private void CheckHealth()
    {
        _mailboxes.RefreshCounts(); // el contador "hoy" cambia a medianoche
        if (Config.Pausado) return;

        var destinations = Config.Reglas.Where(r => r.Activa).Select(r => r.CarpetaDestino)
            .Concat(Config.Buzones.Where(b => b.Activo).Select(b => b.CarpetaDestino))
            .Concat(Config.Limpiezas.Where(c => c.Activa).Select(c => c.CarpetaDestino));
        var now = DateTime.UtcNow;
        var threshold = TimeSpan.FromHours(Config.AlertaHoras);
        var longDown = _monitor.Check(destinations, now).Where(d => now - d.Value >= threshold).ToList();

        if (longDown.Count == 0)
        {
            _lastAlertUtc = DateTime.MinValue;
            return;
        }
        if (now - _lastAlertUtc < threshold || (_alert != null && !_alert.IsDisposed)) return;

        _lastAlertUtc = now;
        _alert = new AlertForm(longDown, Config.Contacto, Organizer.Pending.Count + Organizer.MailboxPendingCount());
        _alert.Show();
        _alert.Activate();
    }

    // ---------- Deshacer ----------

    private void RefreshUndoItem()
    {
        try { _undoable = Organizer.LastUndoable(Log.ReadAll()); }
        catch (Exception) { _undoable = null; }

        _undoItem.Enabled = _undoable != null;
        if (_undoable == null)
        {
            _undoItem.Text = "Deshacer último movimiento";
            return;
        }
        var name = Path.GetFileName(_undoable.Destino);
        if (name.Length > 40) name = name.Substring(0, 37) + "...";
        _undoItem.Text = $"Deshacer: {name}";
    }

    private void UndoLast()
    {
        var entry = _undoable;
        if (entry == null || !RequirePin(null, "deshacer un movimiento")) return;
        var name = Path.GetFileName(entry.Destino);
        var originDir = Path.GetDirectoryName(entry.Origen) ?? "";
        if (!FormKit.Confirm(null, $"¿Devolver \"{name}\" a su carpeta original?\n\n" +
                                   "Ese archivo no se va a volver a mover automáticamente."))
            return;

        var r = Organizer.Undo(entry);
        if (r.Estado == MoveStatus.Deshecho)
        {
            SaveConfig(); // guarda la lista de excluidos
            Balloon("Movimiento deshecho", $"{Path.GetFileName(r.Destino)} volvió a {Path.GetFileName(Path.GetDirectoryName(r.Destino))}");
        }
        else
        {
            FormKit.Warn(null, "No se pudo deshacer: " + r.Detalle);
        }
        _logForm?.Reload();
        _mailboxes.RefreshCounts();
    }

    // ---------- Ventanas ----------

    public void ShowMain()
    {
        if (_main == null || _main.IsDisposed)
        {
            if (!RequirePin(null, "abrir el panel de configuración")) return;
            _main = new MainForm(this);
            _main.FormClosed += (_, _) => _main = null;
        }
        Reveal(_main);
    }

    public void ShowLog()
    {
        if (_logForm == null || _logForm.IsDisposed)
        {
            _logForm = new LogForm(this);
            _logForm.FormClosed += (_, _) => _logForm = null;
        }
        Reveal(_logForm);
    }

    private static void Reveal(Form form)
    {
        form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Activate();
    }

    public static void OpenInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path))
                Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch (Exception) { }
    }

    private void ExitApp()
    {
        if (!RequirePin(null, "cerrar OrdenaPC")) return;
        if (!Config.NoReabrir && !FormKit.Confirm(null,
                "OrdenaPC se va a cerrar y no se vuelve a abrir sola hasta que la abras vos o se reinicie la computadora.\n\n" +
                "Mientras tanto no se ordena nada y los buzones desaparecen. ¿Cerrar?"))
            return;

        Config.Detenido = true;
        TrySave();
        _healthTimer.Stop();
        _watcher.Dispose();
        _mailboxes.CloseAll();
        _alert?.Close();
        _main?.Close();
        _logForm?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }
}
