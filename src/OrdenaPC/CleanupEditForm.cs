using OrdenaPC.Core;

namespace OrdenaPC;

sealed class CleanupEditForm : Form
{
    private readonly TextBox _origin = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _days = new() { Minimum = 1, Maximum = 3650, Width = 70 };
    private readonly TextBox _dest = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _byMonth = new() { Text = "Separar en subcarpetas por mes (por ejemplo 2026-09)", AutoSize = true };

    public CleanupEditForm(CleanupRule cleanup)
    {
        Result = cleanup;
        var t = FormKit.Dialog(this, string.IsNullOrEmpty(cleanup.CarpetaOrigen) ? "Nueva limpieza" : "Editar limpieza");

        FormKit.Row(t, "Carpeta a limpiar:", _origin, FormKit.Browse(_origin, "Elegí la carpeta a mantener limpia"));
        FormKit.Row(t, "", FormKit.QuickFolders(_origin));
        var days = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        days.Controls.Add(_days);
        days.Controls.Add(new Label { Text = "días sin cambios", AutoSize = true, Margin = new Padding(6, 6, 3, 3) });
        FormKit.Row(t, "Mover después de:", days);
        FormKit.Hint(t, "Solo se mueven los archivos que no coinciden con ninguna regla. Nunca se borra nada.");
        FormKit.Row(t, "Carpeta destino:", _dest, FormKit.Browse(_dest, "Elegí a dónde mover los archivos viejos"));
        FormKit.Hint(t, @"Ej.: G:\Mi unidad\Sin clasificar");
        FormKit.Row(t, "", _byMonth);
        FormKit.OkCancel(this, t, Accept);

        _origin.Text = cleanup.CarpetaOrigen;
        _days.Value = Math.Max(_days.Minimum, Math.Min(_days.Maximum, cleanup.Dias));
        _dest.Text = cleanup.CarpetaDestino;
        _byMonth.Checked = cleanup.SubcarpetaPorMes;
    }

    public CleanupRule Result { get; }

    private bool Accept()
    {
        var origin = _origin.Text.Trim();
        var dest = _dest.Text.Trim();
        if (origin.Length == 0) { FormKit.Warn(this, "Indicá la carpeta a limpiar."); return false; }
        if (dest.Length == 0) { FormKit.Warn(this, "Indicá la carpeta destino."); return false; }
        if (string.Equals(TextUtil.TryNormalizeFolder(origin), TextUtil.TryNormalizeFolder(dest), StringComparison.OrdinalIgnoreCase))
        { FormKit.Warn(this, "La carpeta destino no puede ser la misma que la que se limpia."); return false; }

        Result.CarpetaOrigen = origin;
        Result.Dias = (int)_days.Value;
        Result.CarpetaDestino = dest;
        Result.SubcarpetaPorMes = _byMonth.Checked;
        return true;
    }
}
