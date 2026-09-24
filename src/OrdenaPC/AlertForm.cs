namespace OrdenaPC;

/// <summary>Aviso grande, pensado para que nadie lo ignore, cuando un destino (Drive) lleva horas sin acceso.</summary>
sealed class AlertForm : Form
{
    public AlertForm(IReadOnlyList<KeyValuePair<string, DateTime>> down, string contact, int waitingFiles)
    {
        Text = "OrdenaPC — Atención";
        Font = new Font("Segoe UI", 11F);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "⚠  Google Drive no está funcionando",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(198, 40, 40),
            AutoSize = false,
            Size = new Size(640, 64),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Margin = new Padding(0),
        });

        var since = down.Min(d => d.Value).ToLocalTime();
        var sinceText = since.Date == DateTime.Today ? $"las {since:HH:mm}" : $"el {since:dd/MM} a las {since:HH:mm}";
        var lines = new List<string> { $"Desde {sinceText} no se puede acceder a:" };
        lines.AddRange(down.Select(d => "      " + d.Key));
        lines.Add("");
        lines.Add("Los archivos NO se pierden: quedan guardados en esta computadora y se van a " +
                  "mover solos cuando Drive vuelva a funcionar." + (waitingFiles > 0 ? $" ({waitingFiles} esperando)" : ""));
        lines.Add("");
        lines.Add("Por favor, avisá a " + (string.IsNullOrWhiteSpace(contact) ? "quien administra esta computadora." : contact.Trim() + "."));

        root.Controls.Add(new Label
        {
            Text = string.Join("\n", lines), AutoSize = true, MaximumSize = new Size(610, 0), Margin = new Padding(16, 16, 16, 8),
        });

        var ok = new Button
        {
            Text = "Entendido", AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Padding = new Padding(16, 4, 16, 4), Anchor = AnchorStyles.Right, Margin = new Padding(16, 8, 16, 16),
            DialogResult = DialogResult.OK,
        };
        ok.Click += (_, _) => Close();
        root.Controls.Add(ok);
        AcceptButton = ok;
    }
}
