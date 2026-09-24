using OrdenaPC.Core;

namespace OrdenaPC;

sealed class RuleEditForm : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly TextBox _origin = new() { Dock = DockStyle.Fill };
    private readonly TextBox _extensions = new() { Dock = DockStyle.Fill };
    private readonly TextBox _keywords = new() { Dock = DockStyle.Fill };
    private readonly TextBox _dest = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _active = new() { Text = "Regla activa", AutoSize = true };
    private readonly TableLayoutPanel _table = new()
    {
        ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
    };

    public RuleEditForm(Rule rule)
    {
        Result = rule;
        Text = string.IsNullOrEmpty(rule.CarpetaOrigen) ? "Nueva regla" : "Editar regla";
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(_table);

        AddRow("Nombre (opcional):", _name);
        AddRow("Carpeta origen:", _origin, BrowseButton(_origin, "Elegí la carpeta a vigilar"));
        var quick = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        foreach (var (label, path) in new[]
                 {
                     ("Escritorio", KnownFolders.Desktop), ("Descargas", KnownFolders.Downloads), ("Documentos", KnownFolders.Documents),
                 })
        {
            var b = new Button { Text = label, AutoSize = true };
            b.Click += (_, _) => _origin.Text = path;
            quick.Controls.Add(b);
        }
        AddRow("", quick);
        AddRow("Extensiones:", _extensions);
        AddHint("Separadas por coma. Ej.: .docx, .pdf");
        AddRow("Palabras clave:", _keywords);
        AddHint("El nombre del archivo tiene que contener alguna. Sin distinguir mayúsculas ni acentos. Ej.: epicrisis, alta");
        AddRow("Carpeta destino:", _dest, BrowseButton(_dest, "Elegí a dónde mover los archivos"));
        AddHint(@"Típicamente una carpeta de Google Drive. Ej.: G:\Mi unidad\Epicrisis");
        AddRow("", _active);

        var ok = new Button { Text = "Guardar", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Accept();
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        _table.Controls.Add(buttons, 0, _table.RowCount++);
        _table.SetColumnSpan(buttons, 3);
        AcceptButton = ok;
        CancelButton = cancel;

        _name.Text = rule.Nombre;
        _origin.Text = rule.CarpetaOrigen;
        _extensions.Text = string.Join(", ", rule.Extensiones);
        _keywords.Text = string.Join(", ", rule.PalabrasClave);
        _dest.Text = rule.CarpetaDestino;
        _active.Checked = rule.Activa;
    }

    public Rule Result { get; }

    private void AddRow(string label, Control control, Control? extra = null)
    {
        int row = _table.RowCount++;
        _table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, row);
        _table.Controls.Add(control, 1, row);
        if (extra != null) _table.Controls.Add(extra, 2, row);
    }

    private void AddHint(string text)
    {
        int row = _table.RowCount++;
        _table.Controls.Add(new Label
        {
            Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(440, 0),
            Margin = new Padding(3, 0, 3, 8),
        }, 1, row);
    }

    private Button BrowseButton(TextBox target, string description)
    {
        var b = new Button { Text = "Examinar…", AutoSize = true };
        b.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true };
            if (Directory.Exists(target.Text)) dialog.SelectedPath = target.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
        };
        return b;
    }

    private void Accept()
    {
        var extensions = TextUtil.SplitList(_extensions.Text).Select(TextUtil.NormalizeExtension).Distinct().ToList();
        var keywords = TextUtil.SplitList(_keywords.Text);
        var origin = _origin.Text.Trim();
        var dest = _dest.Text.Trim();

        if (origin.Length == 0) { Warn("Indicá la carpeta origen."); return; }
        if (extensions.Count == 0) { Warn("Indicá al menos una extensión (por ejemplo .docx)."); return; }
        if (dest.Length == 0) { Warn("Indicá la carpeta destino."); return; }
        if (string.Equals(TextUtil.TryNormalizeFolder(origin), TextUtil.TryNormalizeFolder(dest), StringComparison.OrdinalIgnoreCase))
        { Warn("La carpeta destino no puede ser la misma que la de origen."); return; }
        if (!Directory.Exists(origin) && !Confirm("La carpeta origen no existe en este momento. ¿Guardar igual?")) return;
        if (keywords.Count == 0 &&
            !Confirm($"Sin palabras clave, esta regla mueve TODOS los archivos {string.Join(", ", extensions)} de la carpeta origen.\n\n¿Seguro?"))
            return;

        Result.Nombre = _name.Text.Trim();
        Result.CarpetaOrigen = origin;
        Result.Extensiones = extensions;
        Result.PalabrasClave = keywords;
        Result.CarpetaDestino = dest;
        Result.Activa = _active.Checked;
        DialogResult = DialogResult.OK;
    }

    private void Warn(string text) =>
        MessageBox.Show(this, text, "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private bool Confirm(string text) =>
        MessageBox.Show(this, text, "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
