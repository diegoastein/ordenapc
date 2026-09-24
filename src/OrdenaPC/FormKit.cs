namespace OrdenaPC;

/// <summary>Piezas comunes de los diálogos de edición (etiqueta + campo, examinar carpeta, Guardar/Cancelar).</summary>
static class FormKit
{
    public const int FieldWidth = 440;

    public static TableLayoutPanel Dialog(Form form, string title)
    {
        form.Text = title;
        form.Font = new Font("Segoe UI", 9F);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.Padding = new Padding(12);

        var table = new TableLayoutPanel
        {
            ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldWidth));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.Controls.Add(table);
        return table;
    }

    public static void Row(TableLayoutPanel t, string label, Control control, Control? extra = null)
    {
        int row = t.RowCount++;
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, row);
        t.Controls.Add(control, 1, row);
        if (extra != null) t.Controls.Add(extra, 2, row);
    }

    public static void Hint(TableLayoutPanel t, string text)
    {
        int row = t.RowCount++;
        t.Controls.Add(new Label
        {
            Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(FieldWidth, 0),
            Margin = new Padding(3, 0, 3, 8),
        }, 1, row);
    }

    public static Button Browse(TextBox target, string description)
    {
        var b = new Button { Text = "Examinar…", AutoSize = true };
        b.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true };
            if (Directory.Exists(target.Text)) dialog.SelectedPath = target.Text;
            if (dialog.ShowDialog(target.FindForm()) == DialogResult.OK) target.Text = dialog.SelectedPath;
        };
        return b;
    }

    /// <summary>Botones Escritorio / Descargas / Documentos que completan el campo.</summary>
    public static FlowLayoutPanel QuickFolders(TextBox target)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        foreach (var (label, path) in new[]
                 {
                     ("Escritorio", KnownFolders.Desktop), ("Descargas", KnownFolders.Downloads), ("Documentos", KnownFolders.Documents),
                 })
        {
            var b = new Button { Text = label, AutoSize = true };
            b.Click += (_, _) => target.Text = path;
            panel.Controls.Add(b);
        }
        return panel;
    }

    /// <summary>Guardar / Cancelar. onOk devuelve true si los datos son válidos y el diálogo se cierra.</summary>
    public static void OkCancel(Form form, TableLayoutPanel t, Func<bool> onOk, string okText = "Guardar")
    {
        var ok = new Button { Text = okText, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (onOk()) form.DialogResult = DialogResult.OK; };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        t.Controls.Add(buttons, 0, t.RowCount++);
        t.SetColumnSpan(buttons, 3);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
    }

    public static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
        };
    }

    public static void Warn(IWin32Window? owner, string text) =>
        MessageBox.Show(owner, text, "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(IWin32Window? owner, string text) =>
        MessageBox.Show(owner, text, "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
