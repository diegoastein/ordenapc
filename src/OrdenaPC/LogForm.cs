using System.Diagnostics;
using OrdenaPC.Core;

namespace OrdenaPC;

sealed class LogForm : Form
{
    private const int MaxRows = 2000;
    private readonly TrayContext _app;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };

    public LogForm(TrayContext app)
    {
        _app = app;
        Text = "OrdenaPC — Log de movimientos";
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 560);
        MinimumSize = new Size(700, 300);
        Icon = TrayIcons.Make(Color.FromArgb(0, 120, 212));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _grid.Columns.Add("fecha", "Fecha");
        _grid.Columns.Add("estado", "Estado");
        _grid.Columns.Add("regla", "Regla");
        _grid.Columns.Add("archivo", "Archivo");
        _grid.Columns.Add("origen", "Origen");
        _grid.Columns.Add("destino", "Destino");
        _grid.Columns.Add("detalle", "Detalle");
        float[] weights = { 12, 8, 10, 18, 20, 22, 20 };
        for (int i = 0; i < weights.Length; i++) _grid.Columns[i].FillWeight = weights[i];
        root.Controls.Add(_grid);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        void Add(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true };
            b.Click += (_, _) => onClick();
            buttons.Controls.Add(b);
        }
        Add("Actualizar", Reload);
        Add("Deshacer seleccionado", UndoSelected);
        Add("Mostrar en carpeta", ShowSelected);
        Add("Abrir log (CSV)", OpenCsv);
        root.Controls.Add(buttons);

        Reload();
    }

    public void Reload()
    {
        var entries = _app.Log.ReadAll();
        _grid.Rows.Clear();
        foreach (var e in Enumerable.Reverse(entries).Take(MaxRows))
        {
            int i = _grid.Rows.Add(e.Fecha.ToString("yyyy-MM-dd HH:mm"), e.Estado.ToString(), e.Regla,
                Path.GetFileName(e.Origen), Path.GetDirectoryName(e.Origen), e.Destino, e.Detalle);
            _grid.Rows[i].Tag = e;
            if (e.Estado is MoveStatus.Error or MoveStatus.Pendiente)
                _grid.Rows[i].DefaultCellStyle.ForeColor = Color.DarkOrange;
        }
    }

    private MoveResult? Selected() => _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as MoveResult : null;

    private void UndoSelected()
    {
        var entry = Selected();
        if (entry == null || entry.Estado != MoveStatus.Movido)
        {
            MessageBox.Show(this, "Seleccioná un movimiento con estado \"Movido\".", "OrdenaPC",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var answer = MessageBox.Show(this,
            $"¿Devolver \"{Path.GetFileName(entry.Destino)}\" a\n{Path.GetDirectoryName(entry.Origen)}?\n\n" +
            "Ese archivo no se va a volver a mover automáticamente.",
            "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        var r = _app.Organizer.Undo(entry);
        if (r.Estado == MoveStatus.Deshecho)
        {
            _app.SaveConfig(); // guarda la lista de excluidos
            MessageBox.Show(this, "Listo, el archivo volvió a su carpeta original.", "OrdenaPC",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(this, "No se pudo deshacer: " + r.Detalle, "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Reload();
    }

    private void ShowSelected()
    {
        var entry = Selected();
        if (entry == null) return;
        TrayContext.OpenInExplorer(File.Exists(entry.Destino) ? entry.Destino : entry.Origen);
    }

    private void OpenCsv()
    {
        if (!File.Exists(_app.Log.FilePath))
        {
            MessageBox.Show(this, "Todavía no hay movimientos registrados.", "OrdenaPC");
            return;
        }
        try { Process.Start(new ProcessStartInfo(_app.Log.FilePath) { UseShellExecute = true }); }
        catch (Exception) { TrayContext.OpenInExplorer(_app.Log.FilePath); }
    }
}
