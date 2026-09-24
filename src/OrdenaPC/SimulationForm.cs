using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>Muestra "qué movería" sin haber tocado ningún archivo. Con okText aparece el botón para ejecutar.</summary>
sealed class SimulationForm : Form
{
    public SimulationForm(IReadOnlyList<MoveResult> results, string title, string? okText, string cancelText)
    {
        Text = title;
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(1000, 520);
        MinimumSize = new Size(600, 300);

        int toMove = results.Count(r => r.Estado == MoveStatus.Simulado);
        int unavailable = results.Count(r => r.Estado == MoveStatus.Pendiente);
        var summary = results.Count == 0
            ? "Ningún archivo coincide con las reglas en este momento."
            : $"Se moverían {toMove} archivo(s)." +
              (unavailable > 0 ? $" {unavailable} tienen el destino no disponible y quedarían esperando." : "") +
              " Todavía no se movió nada.";

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = summary, AutoSize = true, Margin = new Padding(3, 3, 3, 8) });

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
        };
        grid.Columns.Add("archivo", "Archivo");
        grid.Columns.Add("origen", "Carpeta origen");
        grid.Columns.Add("regla", "Regla");
        grid.Columns.Add("destino", "Destino");
        grid.Columns.Add("nota", "Nota");
        grid.Columns[0].FillWeight = 22;
        grid.Columns[1].FillWeight = 18;
        grid.Columns[2].FillWeight = 12;
        grid.Columns[3].FillWeight = 30;
        grid.Columns[4].FillWeight = 18;
        foreach (var r in results.OrderBy(r => r.Estado).ThenBy(r => r.Origen))
        {
            var note = r.Estado switch
            {
                MoveStatus.Simulado => r.Detalle.Length > 0 ? r.Detalle : "Se movería",
                MoveStatus.Pendiente => "Destino no disponible",
                _ => $"{r.Estado}: {r.Detalle}",
            };
            int i = grid.Rows.Add(Path.GetFileName(r.Origen), Path.GetDirectoryName(r.Origen), r.Regla, r.Destino, note);
            if (r.Estado != MoveStatus.Simulado) grid.Rows[i].DefaultCellStyle.ForeColor = Color.DarkOrange;
        }
        root.Controls.Add(grid);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var cancel = new Button { Text = cancelText, AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        CancelButton = cancel;
        if (okText != null)
        {
            var ok = new Button { Text = okText, AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(ok);
        }
        root.Controls.Add(buttons);
    }
}
