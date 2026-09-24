using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>Crea, actualiza y cierra las ventanitas de los buzones según la configuración.</summary>
sealed class MailboxManager
{
    private readonly TrayContext _app;
    private readonly Dictionary<Guid, MailboxForm> _forms = new();

    public MailboxManager(TrayContext app) => _app = app;

    /// <summary>En modo acomodar, los buzones se arrastran para moverlos y agrandarlos.</summary>
    public bool EditMode { get; private set; }

    public void Reload()
    {
        var active = _app.Config.Buzones.Where(b => b.Activo).ToList();
        foreach (var id in _forms.Keys.Where(id => active.All(b => b.Id != id)).ToList())
        {
            _forms[id].Close();
            _forms.Remove(id);
        }

        for (int i = 0; i < active.Count; i++)
        {
            var box = active[i];
            if (!IsOnScreen(box))
            {
                var p = DefaultLocation(i, box);
                box.X = p.X;
                box.Y = p.Y;
            }
            if (_forms.TryGetValue(box.Id, out var form))
            {
                form.ApplyBox(box, keepBounds: EditMode);
            }
            else
            {
                form = new MailboxForm(_app, box);
                form.SetEditMode(EditMode);
                _forms[box.Id] = form;
                form.Show();
            }
        }
        RefreshCounts();
    }

    public void SetEditMode(bool on)
    {
        if (on == EditMode) return;
        EditMode = on;
        foreach (var form in _forms.Values)
        {
            form.SetEditMode(on);
            if (!on)
            {
                form.Box.X = form.Left;
                form.Box.Y = form.Top;
                form.Box.Ancho = form.Width;
                form.Box.Alto = form.Height;
            }
        }
        if (on) ShowAll();
        else _app.SaveConfig();
    }

    /// <summary>Trae los buzones al frente, por si quedaron tapados por otras ventanas.</summary>
    public void ShowAll()
    {
        foreach (var form in _forms.Values)
        {
            form.Show();
            form.TopMost = true;
            form.TopMost = false;
        }
    }

    public void RefreshCounts()
    {
        if (_forms.Count == 0) return;
        List<MoveResult> log;
        try { log = _app.Log.ReadAll(); }
        catch (Exception) { return; }
        var today = DateTime.Today;
        foreach (var form in _forms.Values)
        {
            var rule = form.Box.NombreRegla;
            form.SetTodayCount(log.Count(e => e.Estado == MoveStatus.Movido && e.Fecha.Date == today && e.Regla == rule));
        }
    }

    public void CloseAll()
    {
        foreach (var form in _forms.Values) form.Close();
        _forms.Clear();
    }

    private static bool IsOnScreen(Mailbox box)
    {
        if (box.X < 0 && box.Y < 0) return false;
        var rect = new Rectangle(box.X, box.Y, box.Ancho, box.Alto);
        return Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect));
    }

    // Columna sobre el borde derecho de la pantalla principal; si no entran, sigue en otra columna.
    private static Point DefaultLocation(int index, Mailbox box)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 728);
        int perColumn = Math.Max(1, (area.Height - 20) / (box.Alto + 15));
        int column = index / perColumn, row = index % perColumn;
        return new Point(area.Right - (box.Ancho + 20) * (column + 1), area.Top + 20 + row * (box.Alto + 15));
    }
}
