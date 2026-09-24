using OrdenaPC.Core;

namespace OrdenaPC;

sealed class MailboxEditForm : Form
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(46, 125, 50), Color.FromArgb(21, 101, 192), Color.FromArgb(198, 40, 40), Color.FromArgb(239, 108, 0),
        Color.FromArgb(106, 27, 154), Color.FromArgb(0, 131, 143), Color.FromArgb(249, 168, 37), Color.FromArgb(84, 110, 122),
    };

    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly Panel _preview = new() { Size = new Size(60, 26), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3, 3, 8, 3) };
    private readonly NumericUpDown _width = new() { Minimum = 120, Maximum = 800, Width = 70 };
    private readonly NumericUpDown _height = new() { Minimum = 80, Maximum = 600, Width = 70 };
    private readonly TextBox _dest = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _active = new() { Text = "Mostrar en el escritorio", AutoSize = true };

    public MailboxEditForm(Mailbox box)
    {
        Result = box;
        var t = FormKit.Dialog(this, string.IsNullOrEmpty(box.Nombre) ? "Nuevo buzón" : "Editar buzón");

        FormKit.Row(t, "Nombre:", _name);
        FormKit.Hint(t, "Es el título grande del buzón. Ej.: Epicrisis");

        var colors = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        colors.Controls.Add(_preview);
        foreach (var c in Palette)
        {
            var swatch = new Button { BackColor = c, Size = new Size(26, 26), FlatStyle = FlatStyle.Flat, Margin = new Padding(2) };
            swatch.FlatAppearance.BorderSize = 0;
            swatch.Click += (_, _) => _preview.BackColor = c;
            colors.Controls.Add(swatch);
        }
        var more = new Button { Text = "Otro…", AutoSize = true };
        more.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _preview.BackColor, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) _preview.BackColor = dialog.Color;
        };
        colors.Controls.Add(more);
        FormKit.Row(t, "Color:", colors);

        var size = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        size.Controls.Add(new Label { Text = "Ancho", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        size.Controls.Add(_width);
        size.Controls.Add(new Label { Text = "Alto", AutoSize = true, Margin = new Padding(12, 6, 3, 3) });
        size.Controls.Add(_height);
        size.Controls.Add(new Label { Text = "píxeles", AutoSize = true, Margin = new Padding(6, 6, 3, 3) });
        FormKit.Row(t, "Tamaño:", size);
        FormKit.Hint(t, "También se puede cambiar con \"Acomodar en el escritorio\", arrastrando la esquina.");

        FormKit.Row(t, "Carpeta destino:", _dest, FormKit.Browse(_dest, "Elegí a dónde van los archivos de este buzón"));
        FormKit.Hint(t, @"Lo que se suelta en el buzón va a esta carpeta. Ej.: G:\Mi unidad\Epicrisis");
        FormKit.Row(t, "", _active);
        FormKit.OkCancel(this, t, Accept);

        _name.Text = box.Nombre;
        _preview.BackColor = Color.FromArgb(box.Color);
        _width.Value = Math.Max(_width.Minimum, Math.Min(_width.Maximum, box.Ancho));
        _height.Value = Math.Max(_height.Minimum, Math.Min(_height.Maximum, box.Alto));
        _dest.Text = box.CarpetaDestino;
        _active.Checked = box.Activo;
    }

    public Mailbox Result { get; }

    private bool Accept()
    {
        if (_name.Text.Trim().Length == 0) { FormKit.Warn(this, "Poné un nombre al buzón."); return false; }
        if (_dest.Text.Trim().Length == 0) { FormKit.Warn(this, "Indicá la carpeta destino."); return false; }
        if (!SafeMover.IsAvailable(TextUtil.TryNormalizeFolder(_dest.Text)) &&
            !FormKit.Confirm(this, "La carpeta destino no está disponible en este momento. ¿Guardar igual?"))
            return false;

        Result.Nombre = _name.Text.Trim();
        Result.Color = Color.FromArgb(255, _preview.BackColor).ToArgb();
        Result.Ancho = (int)_width.Value;
        Result.Alto = (int)_height.Value;
        Result.CarpetaDestino = _dest.Text.Trim();
        Result.Activo = _active.Checked;
        return true;
    }
}
