using OrdenaPC.Core;

namespace OrdenaPC;

sealed class MainForm : Form
{
    private static readonly int[] Intervals = { 15, 30, 60, 120 };

    private readonly TrayContext _app;
    private readonly DataGridView _grid = new();
    private readonly ComboBox _interval = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox _notifications = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly CheckBox _autostart = new() { Text = "Iniciar con Windows", AutoSize = true, Margin = new Padding(16, 6, 3, 3) };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    private readonly FlowLayoutPanel _banner = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 244, 206), Padding = new Padding(6),
    };
    private bool _loading;

    public MainForm(TrayContext app)
    {
        _app = app;
        Text = "OrdenaPC — Reglas";
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1050, 580);
        MinimumSize = new Size(760, 400);
        Icon = TrayIcons.Make(Color.FromArgb(0, 120, 212));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        BuildBanner();
        root.Controls.Add(_banner);
        root.Controls.Add(_status);
        BuildGrid();
        root.Controls.Add(_grid);
        root.Controls.Add(BuildButtons());
        root.Controls.Add(BuildOptions());

        LoadRules();
        LoadOptions();
        UpdateStatus();
        _app.StateChanged += UpdateStatus;
        FormClosed += (_, _) => _app.StateChanged -= UpdateStatus;
    }

    private void BuildBanner()
    {
        _banner.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(3, 7, 12, 3),
            Text = "La vigilancia automática está en espera hasta que revises qué movería el primer barrido.",
        });
        var review = new Button { Text = "Revisar primer barrido", AutoSize = true };
        review.Click += async (_, _) => await Busy(() => _app.ReviewFirstSweepAsync(this));
        _banner.Controls.Add(review);
        var skip = new LinkLabel { Text = "Activar sin simular", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
        skip.LinkClicked += (_, _) => _app.ActivateWithoutReview(this);
        _banner.Controls.Add(skip);
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;

        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Activa", FillWeight = 7 });
        AddColumn("Nombre", 14);
        AddColumn("Carpeta origen", 20);
        AddColumn("Extensiones", 11);
        AddColumn("Palabras clave", 15);
        AddColumn("Carpeta destino", 30);

        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0 || _grid.Rows[e.RowIndex].Tag is not Rule rule) return;
            rule.Activa = !rule.Activa;
            SaveAndReload();
        };
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) EditSelected(); };
    }

    private void AddColumn(string header, float weight) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, ReadOnly = true });

    private FlowLayoutPanel BuildButtons()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        void Add(string text, EventHandler onClick, int leftGap = 3)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(leftGap, 3, 3, 3) };
            b.Click += onClick;
            panel.Controls.Add(b);
        }

        Add("Agregar regla", (_, _) => AddRule());
        Add("Editar", (_, _) => EditSelected());
        Add("Eliminar", (_, _) => DeleteSelected());
        Add("▲ Subir", (_, _) => MoveSelected(-1));
        Add("▼ Bajar", (_, _) => MoveSelected(+1));
        Add("Probar regla", async (_, _) => await Busy(TestSelectedAsync), leftGap: 24);
        Add("Simular todo", async (_, _) => await Busy(SimulateAllAsync));
        Add("Ejecutar ahora", async (_, _) => await Busy(() => _app.RunNowAsync(this)));
        Add("Ver log", (_, _) => _app.ShowLog(), leftGap: 24);
        return panel;
    }

    private FlowLayoutPanel BuildOptions()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        panel.Controls.Add(new Label { Text = "Barrido de respaldo cada", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        _interval.Items.AddRange(Intervals.Select(i => (object)$"{i} min").ToArray());
        panel.Controls.Add(_interval);
        panel.Controls.Add(new Label { Text = "Notificaciones:", AutoSize = true, Margin = new Padding(16, 7, 3, 3) });
        _notifications.Items.AddRange(new object[] { "Nunca", "Por cada archivo movido", "Resumen al terminar cada barrido" });
        panel.Controls.Add(_notifications);
        panel.Controls.Add(_autostart);
        var dataLink = new LinkLabel { Text = "Abrir carpeta de datos", AutoSize = true, Margin = new Padding(16, 7, 3, 3) };
        dataLink.LinkClicked += (_, _) => { Directory.CreateDirectory(AppConfig.DataDir); TrayContext.OpenInExplorer(AppConfig.DataDir); };
        panel.Controls.Add(dataLink);

        _interval.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _app.Config.IntervaloBarridoMin = Intervals[_interval.SelectedIndex];
            _app.SaveConfig();
        };
        _notifications.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _app.Config.Notificaciones = (NotificationMode)_notifications.SelectedIndex;
            _app.SaveConfig();
        };
        _autostart.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            try { Autostart.Set(_autostart.Checked); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        return panel;
    }

    private void LoadOptions()
    {
        _loading = true;
        var idx = Array.IndexOf(Intervals, _app.Config.IntervaloBarridoMin);
        _interval.SelectedIndex = idx >= 0 ? idx : 1;
        _notifications.SelectedIndex = (int)_app.Config.Notificaciones;
        _autostart.Checked = Autostart.IsEnabled();
        _loading = false;
    }

    private void LoadRules()
    {
        var selectedId = SelectedRule()?.Id;
        _grid.Rows.Clear();
        foreach (var r in _app.Config.Reglas)
        {
            int i = _grid.Rows.Add(r.Activa, r.Nombre, r.CarpetaOrigen, string.Join(", ", r.Extensiones),
                string.Join(", ", r.PalabrasClave), r.CarpetaDestino);
            _grid.Rows[i].Tag = r;
            _grid.Rows[i].DefaultCellStyle.ForeColor = r.Activa ? SystemColors.ControlText : SystemColors.GrayText;
        }
        _grid.ClearSelection();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is Rule r && r.Id == selectedId) { row.Selected = true; _grid.CurrentCell = row.Cells[1]; }
        }
    }

    private void UpdateStatus()
    {
        _status.Text = "Estado: " + _app.StatusText;
        _banner.Visible = _app.Config.Reglas.Any(r => r.Activa) && !_app.Config.PrimerBarridoHecho;
    }

    private Rule? SelectedRule() => _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as Rule : null;

    private Rule? RequireSelected()
    {
        var rule = SelectedRule();
        if (rule == null)
            MessageBox.Show(this, "Seleccioná una regla de la tabla.", "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return rule;
    }

    private void SaveAndReload()
    {
        _app.SaveConfig();
        LoadRules();
    }

    private void AddRule()
    {
        using var form = new RuleEditForm(new Rule());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Reglas.Add(form.Result);
        SaveAndReload();
        SelectRule(form.Result);
    }

    private void EditSelected()
    {
        var rule = RequireSelected();
        if (rule == null) return;
        using var form = new RuleEditForm(rule.Clone());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var i = _app.Config.Reglas.IndexOf(rule);
        _app.Config.Reglas[i] = form.Result;
        SaveAndReload();
    }

    private void DeleteSelected()
    {
        var rule = RequireSelected();
        if (rule == null) return;
        var answer = MessageBox.Show(this, $"¿Eliminar la regla \"{rule.NombreVisible}\"?\n\nNo se toca ningún archivo.",
            "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        _app.Config.Reglas.Remove(rule);
        SaveAndReload();
    }

    private void MoveSelected(int delta)
    {
        var rule = RequireSelected();
        if (rule == null) return;
        var rules = _app.Config.Reglas;
        int i = rules.IndexOf(rule), j = i + delta;
        if (j < 0 || j >= rules.Count) return;
        (rules[i], rules[j]) = (rules[j], rules[i]);
        SaveAndReload();
    }

    private void SelectRule(Rule rule)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag == rule) { _grid.ClearSelection(); row.Selected = true; _grid.CurrentCell = row.Cells[1]; }
        }
    }

    private async Task TestSelectedAsync()
    {
        var rule = RequireSelected();
        if (rule == null) return;
        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true, onlyRule: rule));
        using var form = new SimulationForm(results, $"Prueba de la regla \"{rule.NombreVisible}\"", null, "Cerrar");
        form.ShowDialog(this);
    }

    private async Task SimulateAllAsync()
    {
        if (!_app.Config.PrimerBarridoHecho)
        {
            await _app.ReviewFirstSweepAsync(this);
            return;
        }
        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true));
        bool any = results.Any(r => r.Estado == MoveStatus.Simulado);
        using (var form = new SimulationForm(results, "Simulación de todas las reglas activas", any ? "Ejecutar ahora" : null, "Cerrar"))
        {
            if (form.ShowDialog(this) != DialogResult.OK) return;
        }
        await _app.RunNowAsync(this);
    }

    private async Task Busy(Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try { await action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }
}
