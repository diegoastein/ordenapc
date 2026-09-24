using System.Diagnostics;
using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>Contexto de la app: ícono en la bandeja, configuración, watcher y ventanas.</summary>
sealed class TrayContext : ApplicationContext
{
    private readonly WatcherService _watcher;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly Control _ui = new();
    private readonly Icon _iconOn = TrayIcons.Make(Color.FromArgb(0, 120, 212));
    private readonly Icon _iconOff = TrayIcons.Make(Color.Gray);
    private MainForm? _main;
    private LogForm? _logForm;
    private DateTime _lastPendingNotice = DateTime.MinValue;

    public TrayContext()
    {
        _ = _ui.Handle; // crea el handle para poder volver al hilo de la UI desde los timers

        Config = AppConfig.Load(ConfigPath);
        Log = new MoveLog(Path.Combine(AppConfig.DataDir, "log.csv"));
        Organizer = new Organizer(() => Config, new SafeMover(), Log);
        _watcher = new WatcherService(Organizer, () => Config);
        _watcher.Processed += (results, sweep) => _ui.BeginInvoke((Action)(() => OnProcessed(results, sweep)));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir panel", null, (_, _) => ShowMain());
        menu.Items.Add("Ejecutar ahora", null, async (_, _) => await RunNowAsync(null));
        _pauseItem = new ToolStripMenuItem("Pausar", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Ver log", null, (_, _) => ShowLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApp());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Icon = _iconOff, Text = "OrdenaPC", Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        ApplyState();
        if (Config.Reglas.Count == 0 || !Config.PrimerBarridoHecho)
            _ui.BeginInvoke((Action)ShowMain);
    }

    public AppConfig Config { get; }
    public string ConfigPath { get; } = Path.Combine(AppConfig.DataDir, "config.json");
    public MoveLog Log { get; }
    public Organizer Organizer { get; }

    /// <summary>Cambió el estado (pausa, pendientes, primer barrido); las ventanas se refrescan.</summary>
    public event Action? StateChanged;

    public bool IsActive => !Config.Pausado && Config.PrimerBarridoHecho && Config.Reglas.Any(r => r.Activa);

    public string StatusText =>
        Config.Pausado ? "En pausa" :
        !Config.Reglas.Any(r => r.Activa) ? "Sin reglas activas" :
        !Config.PrimerBarridoHecho ? "En espera: falta revisar el primer barrido" :
        Organizer.Pending.Count > 0 ? $"Vigilando · {Organizer.Pending.Count} pendiente(s) de destino no disponible" :
        "Vigilando";

    /// <summary>Guarda la configuración y reinicia la vigilancia con los cambios.</summary>
    public void SaveConfig()
    {
        try
        {
            Config.Save(ConfigPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo guardar la configuración:\n" + ex.Message, "OrdenaPC",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        ApplyState();
    }

    private void ApplyState()
    {
        _watcher.Stop();
        if (IsActive) _watcher.Start();
        _pauseItem.Text = Config.Pausado ? "Reanudar" : "Pausar";
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
        Config.Pausado = !Config.Pausado;
        SaveConfig();
    }

    public async Task RunNowAsync(IWin32Window? owner)
    {
        if (!Config.PrimerBarridoHecho)
        {
            ShowMain();
            await ReviewFirstSweepAsync(_main);
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
        var answer = MessageBox.Show(owner,
            "La vigilancia se activa ya, sin simulación previa. El primer barrido va a mover todos los archivos " +
            "existentes que coincidan con las reglas.\n\n¿Continuar?",
            "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        Config.PrimerBarridoHecho = true;
        SaveConfig();
    }

    private void OnProcessed(IReadOnlyList<MoveResult> results, bool sweep)
    {
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

        if (moved.Count > 0 || pending > 0) _logForm?.Reload();
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

        var lines = new List<string> { moved == 1 ? "1 archivo ordenado." : $"{moved} archivos ordenados." };
        if (pending > 0) lines.Add($"{pending} esperando a que el destino esté disponible (se reintenta solo).");
        if (inUse > 0) lines.Add($"{inUse} abiertos en otro programa (se reintenta en el próximo barrido).");
        if (errors > 0) lines.Add($"{errors} con error (ver el log).");
        MessageBox.Show(owner, string.Join("\n", lines), "OrdenaPC", MessageBoxButtons.OK,
            errors > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    public void ShowMain()
    {
        if (_main == null || _main.IsDisposed)
        {
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
        _watcher.Dispose();
        _main?.Close();
        _logForm?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }
}
