using OrdenaPC.Core;

namespace OrdenaPC;

sealed class MainForm : Form
{
    private static readonly int[] Intervals = { 15, 30, 60, 120 };
    private static readonly int[] Waits = { 0, 1, 5, 10, 30, 60 };
    private static readonly int[] AlertHours = { 1, 2, 3, 6, 12, 24 };

    private readonly TrayContext _app;
    private readonly DataGridView _rules = FormKit.Grid();
    private readonly DataGridView _boxes = FormKit.Grid();
    private readonly DataGridView _cleanups = FormKit.Grid();
    private readonly ComboBox _interval = Combo(90);
    private readonly ComboBox _wait = Combo(90);
    private readonly ComboBox _notifications = Combo(260);
    private readonly ComboBox _alertHours = Combo(90);
    private readonly TextBox _contact = new() { Width = 260 };
    private readonly CheckBox _autostart = new() { Text = "Iniciar con Windows", AutoSize = true };
    private readonly CheckBox _reopen = new() { Text = "Volver a abrirse sola si alguien la cierra", AutoSize = true };
    private readonly Label _pinStatus = new() { AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
    private readonly Button _removePin = new() { Text = "Quitar PIN", AutoSize = true };
    private readonly Button _arrange = new() { Text = "Acomodar en el escritorio", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    private readonly FlowLayoutPanel _banner = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 244, 206), Padding = new Padding(6),
    };
    private readonly System.Windows.Forms.Timer _unlockTimer = new() { Interval = 60_000 };
    private bool _loading;

    public MainForm(TrayContext app)
    {
        _app = app;
        Text = "OrdenaPC — Configuración";
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1080, 620);
        MinimumSize = new Size(800, 450);
        Icon = TrayIcons.Make(Color.FromArgb(0, 120, 212));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        BuildBanner();
        root.Controls.Add(_banner);
        root.Controls.Add(_status);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
        tabs.TabPages.Add(Page("Reglas", BuildRulesTab()));
        tabs.TabPages.Add(Page("Buzones", BuildMailboxesTab()));
        tabs.TabPages.Add(Page("Limpieza", BuildCleanupTab()));
        tabs.TabPages.Add(Page("Opciones", BuildOptionsTab()));
        root.Controls.Add(tabs);

        LoadAll();
        _app.StateChanged += UpdateStatus;
        // Mientras el panel está abierto, el PIN no vence.
        _app.KeepUnlocked();
        _unlockTimer.Tick += (_, _) => _app.KeepUnlocked();
        _unlockTimer.Start();
        FormClosed += (_, _) =>
        {
            _app.StateChanged -= UpdateStatus;
            _unlockTimer.Dispose();
            if (_app.Mailboxes.EditMode) _app.Mailboxes.SetEditMode(false);
        };
    }

    private static ComboBox Combo(int width) => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8), UseVisualStyleBackColor = true };
        page.Controls.Add(content);
        return page;
    }

    private static TableLayoutPanel TabLayout(string help, DataGridView grid, FlowLayoutPanel buttons)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = help, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 8) });
        t.Controls.Add(grid);
        t.Controls.Add(buttons);
        return t;
    }

    private static FlowLayoutPanel ButtonRow(params (string Text, EventHandler OnClick, int Gap)[] buttons)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        foreach (var (text, onClick, gap) in buttons)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(gap, 3, 3, 3) };
            b.Click += onClick;
            panel.Controls.Add(b);
        }
        return panel;
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

    private void LoadAll()
    {
        LoadRules();
        LoadMailboxes();
        LoadCleanups();
        LoadOptions();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _status.Text = "Estado: " + _app.StatusText;
        _banner.Visible = _app.NeedsFirstSweep;
    }

    private void Save()
    {
        _app.SaveConfig();
        LoadAll();
    }

    // Tilde de "Activa" en la primera columna de cada tabla.
    private static void OnToggle<T>(DataGridView grid, Action<T> toggle, Action save) where T : class
    {
        grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0 || grid.Rows[e.RowIndex].Tag is not T item) return;
            toggle(item);
            save();
        };
    }

    private static T? Selected<T>(DataGridView grid) where T : class =>
        grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Tag as T : null;

    private T? RequireSelected<T>(DataGridView grid, string what) where T : class
    {
        var item = Selected<T>(grid);
        if (item == null)
            MessageBox.Show(this, $"Seleccioná {what} de la tabla.", "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return item;
    }

    private static void SelectTag(DataGridView grid, object? tag)
    {
        grid.ClearSelection();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!ReferenceEquals(row.Tag, tag)) continue;
            row.Selected = true;
            grid.CurrentCell = row.Cells[1];
        }
    }

    // ---------- Reglas ----------

    private Control BuildRulesTab()
    {
        _rules.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Activa", FillWeight = 7 });
        AddColumns(_rules, ("Nombre", 14), ("Carpeta origen", 20), ("Extensiones", 11), ("Palabras clave", 15), ("Carpeta destino", 30));
        OnToggle<Rule>(_rules, r => r.Activa = !r.Activa, Save);
        _rules.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) EditRule(); };

        return TabLayout(
            "Los archivos cuyo nombre contiene una palabra clave y tienen una de las extensiones se mueven a la carpeta destino. " +
            "Si coinciden con varias reglas, se aplica la primera.",
            _rules,
            ButtonRow(
                ("Agregar regla", (_, _) => AddRule(), 3),
                ("Editar", (_, _) => EditRule(), 3),
                ("Eliminar", (_, _) => DeleteRule(), 3),
                ("▲ Subir", (_, _) => MoveRule(-1), 3),
                ("▼ Bajar", (_, _) => MoveRule(+1), 3),
                ("Probar regla", async (_, _) => await Busy(TestRuleAsync), 24),
                ("Simular todo", async (_, _) => await Busy(SimulateAllAsync), 3),
                ("Ejecutar ahora", async (_, _) => await Busy(() => _app.RunNowAsync(this)), 3),
                ("Ver log", (_, _) => _app.ShowLog(), 24)));
    }

    private static void AddColumns(DataGridView grid, params (string Header, float Weight)[] columns)
    {
        foreach (var (header, weight) in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, ReadOnly = true });
    }

    private void LoadRules()
    {
        var selected = Selected<Rule>(_rules);
        _rules.Rows.Clear();
        foreach (var r in _app.Config.Reglas)
        {
            int i = _rules.Rows.Add(r.Activa, r.Nombre, r.CarpetaOrigen, string.Join(", ", r.Extensiones),
                string.Join(", ", r.PalabrasClave), r.CarpetaDestino);
            _rules.Rows[i].Tag = r;
            _rules.Rows[i].DefaultCellStyle.ForeColor = r.Activa ? SystemColors.ControlText : SystemColors.GrayText;
        }
        SelectTag(_rules, selected);
    }

    private void AddRule()
    {
        using var form = new RuleEditForm(new Rule());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Reglas.Add(form.Result);
        Save();
        SelectTag(_rules, form.Result);
    }

    private void EditRule()
    {
        var rule = RequireSelected<Rule>(_rules, "una regla");
        if (rule == null) return;
        using var form = new RuleEditForm(rule.Clone());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Reglas[_app.Config.Reglas.IndexOf(rule)] = form.Result;
        Save();
        SelectTag(_rules, form.Result);
    }

    private void DeleteRule()
    {
        var rule = RequireSelected<Rule>(_rules, "una regla");
        if (rule == null || !FormKit.Confirm(this, $"¿Eliminar la regla \"{rule.NombreVisible}\"?\n\nNo se toca ningún archivo.")) return;
        _app.Config.Reglas.Remove(rule);
        Save();
    }

    private void MoveRule(int delta)
    {
        var rule = RequireSelected<Rule>(_rules, "una regla");
        if (rule == null) return;
        var rules = _app.Config.Reglas;
        int i = rules.IndexOf(rule), j = i + delta;
        if (j < 0 || j >= rules.Count) return;
        (rules[i], rules[j]) = (rules[j], rules[i]);
        Save();
        SelectTag(_rules, rule);
    }

    private async Task TestRuleAsync()
    {
        var rule = RequireSelected<Rule>(_rules, "una regla");
        if (rule == null) return;
        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true, onlyRule: rule));
        using var form = new SimulationForm(results, $"Prueba de la regla \"{rule.NombreVisible}\"", null, "Cerrar");
        form.ShowDialog(this);
    }

    private async Task SimulateAllAsync()
    {
        if (_app.NeedsFirstSweep)
        {
            await _app.ReviewFirstSweepAsync(this);
            return;
        }
        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true));
        bool any = results.Any(r => r.Estado == MoveStatus.Simulado);
        using (var form = new SimulationForm(results, "Simulación de todas las reglas y limpiezas activas",
                   any ? "Ejecutar ahora" : null, "Cerrar"))
        {
            if (form.ShowDialog(this) != DialogResult.OK) return;
        }
        await _app.RunNowAsync(this);
    }

    // ---------- Buzones ----------

    private Control BuildMailboxesTab()
    {
        _boxes.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Visible", FillWeight = 8 });
        AddColumns(_boxes, ("Nombre", 20), ("Color", 8), ("Tamaño", 10), ("Carpeta destino", 44), ("Esperando envío", 10));
        OnToggle<Mailbox>(_boxes, b => b.Activo = !b.Activo, Save);
        _boxes.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) EditMailbox(); };
        _arrange.Click += (_, _) =>
        {
            _app.Mailboxes.SetEditMode(!_app.Mailboxes.EditMode);
            UpdateArrangeButton();
            if (!_app.Mailboxes.EditMode) LoadMailboxes();
        };

        var buttons = ButtonRow(
            ("Agregar buzón", (_, _) => AddMailbox(), 3),
            ("Editar", (_, _) => EditMailbox(), 3),
            ("Eliminar", (_, _) => DeleteMailbox(), 3),
            ("Abrir carpeta destino", (_, _) => OpenMailboxDestination(), 24),
            ("Mostrar buzones", (_, _) => _app.Mailboxes.ShowAll(), 3));
        _arrange.Margin = new Padding(24, 3, 3, 3);
        buttons.Controls.Add(_arrange);

        return TabLayout(
            "Los buzones son recuadros de color en el escritorio. Lo que se arrastra encima va a su carpeta destino, " +
            "sin importar cómo se llame. Si la carpeta no está disponible, queda guardado en la PC y se envía después.",
            _boxes, buttons);
    }

    private void UpdateArrangeButton() =>
        _arrange.Text = _app.Mailboxes.EditMode ? "✓ Listo, guardar posiciones" : "Acomodar en el escritorio";

    private void LoadMailboxes()
    {
        var selected = Selected<Mailbox>(_boxes);
        _boxes.Rows.Clear();
        foreach (var b in _app.Config.Buzones)
        {
            int waiting = _app.Organizer.StagedFiles(b).Count;
            int i = _boxes.Rows.Add(b.Activo, b.Nombre, "", $"{b.Ancho} × {b.Alto}", b.CarpetaDestino,
                waiting > 0 ? waiting.ToString() : "");
            _boxes.Rows[i].Tag = b;
            var colorCell = _boxes.Rows[i].Cells[2];
            colorCell.Style.BackColor = colorCell.Style.SelectionBackColor = Color.FromArgb(b.Color);
            _boxes.Rows[i].DefaultCellStyle.ForeColor = b.Activo ? SystemColors.ControlText : SystemColors.GrayText;
        }
        SelectTag(_boxes, selected);
        UpdateArrangeButton();
    }

    private void AddMailbox()
    {
        using var form = new MailboxEditForm(new Mailbox());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Buzones.Add(form.Result);
        Save();
        SelectTag(_boxes, form.Result);
    }

    private void EditMailbox()
    {
        var box = RequireSelected<Mailbox>(_boxes, "un buzón");
        if (box == null) return;
        if (_app.Mailboxes.EditMode) _app.Mailboxes.SetEditMode(false); // guarda posiciones antes de editar
        using var form = new MailboxEditForm(box.Clone());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Buzones[_app.Config.Buzones.IndexOf(box)] = form.Result;
        Save();
        SelectTag(_boxes, form.Result);
    }

    private void DeleteMailbox()
    {
        var box = RequireSelected<Mailbox>(_boxes, "un buzón");
        if (box == null) return;
        var staged = _app.Organizer.StagedFiles(box);
        if (staged.Count > 0)
        {
            FormKit.Warn(this, $"Este buzón tiene {staged.Count} archivo(s) guardados esperando a que la carpeta destino esté " +
                               "disponible. Esperá a que se envíen (o desactivá el buzón) antes de eliminarlo.");
            return;
        }
        if (!FormKit.Confirm(this, $"¿Eliminar el buzón \"{box.Nombre}\"?\n\nNo se toca ningún archivo.")) return;
        _app.Config.Buzones.Remove(box);
        Save();
    }

    private void OpenMailboxDestination()
    {
        var box = RequireSelected<Mailbox>(_boxes, "un buzón");
        if (box == null) return;
        var dir = TextUtil.TryNormalizeFolder(box.CarpetaDestino);
        if (Directory.Exists(dir)) TrayContext.OpenInExplorer(dir);
        else FormKit.Warn(this, "La carpeta destino no está disponible en este momento.");
    }

    // ---------- Limpieza ----------

    private Control BuildCleanupTab()
    {
        _cleanups.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Activa", FillWeight = 7 });
        AddColumns(_cleanups, ("Carpeta a limpiar", 28), ("Después de", 10), ("Carpeta destino", 40), ("Por mes", 8));
        OnToggle<CleanupRule>(_cleanups, c => c.Activa = !c.Activa, Save);
        _cleanups.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) EditCleanup(); };

        return TabLayout(
            "Los archivos que no coinciden con ninguna regla y llevan más de N días sin cambios se mueven a la carpeta destino. " +
            "Nunca se borra nada. Sirve para que el Escritorio y Descargas no se llenen de archivos sueltos.",
            _cleanups,
            ButtonRow(
                ("Agregar limpieza", async (_, _) => await Busy(AddCleanupAsync), 3),
                ("Editar", (_, _) => EditCleanup(), 3),
                ("Eliminar", (_, _) => DeleteCleanup(), 3),
                ("Probar", async (_, _) => await Busy(TestCleanupAsync), 24)));
    }

    private void LoadCleanups()
    {
        var selected = Selected<CleanupRule>(_cleanups);
        _cleanups.Rows.Clear();
        foreach (var c in _app.Config.Limpiezas)
        {
            int i = _cleanups.Rows.Add(c.Activa, c.CarpetaOrigen, $"{c.Dias} días", c.CarpetaDestino, c.SubcarpetaPorMes ? "Sí" : "No");
            _cleanups.Rows[i].Tag = c;
            _cleanups.Rows[i].DefaultCellStyle.ForeColor = c.Activa ? SystemColors.ControlText : SystemColors.GrayText;
        }
        SelectTag(_cleanups, selected);
    }

    /// <summary>Una limpieza nueva se guarda desactivada y se activa recién después de ver qué movería.</summary>
    private async Task AddCleanupAsync()
    {
        CleanupRule cleanup;
        using (var form = new CleanupEditForm(new CleanupRule()))
        {
            if (form.ShowDialog(this) != DialogResult.OK) return;
            cleanup = form.Result;
        }
        cleanup.Activa = false;
        _app.Config.Limpiezas.Add(cleanup);
        Save();

        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true, onlyCleanup: cleanup));
        using (var sim = new SimulationForm(results, "Nueva limpieza: esto es lo que movería ahora", "Activar limpieza",
                   "Dejarla desactivada"))
        {
            if (sim.ShowDialog(this) == DialogResult.OK)
            {
                cleanup.Activa = true;
                Save();
            }
        }
        SelectTag(_cleanups, cleanup);
    }

    private void EditCleanup()
    {
        var cleanup = RequireSelected<CleanupRule>(_cleanups, "una limpieza");
        if (cleanup == null) return;
        using var form = new CleanupEditForm(cleanup.Clone());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _app.Config.Limpiezas[_app.Config.Limpiezas.IndexOf(cleanup)] = form.Result;
        Save();
        SelectTag(_cleanups, form.Result);
    }

    private void DeleteCleanup()
    {
        var cleanup = RequireSelected<CleanupRule>(_cleanups, "una limpieza");
        if (cleanup == null || !FormKit.Confirm(this, "¿Eliminar esta limpieza?\n\nNo se toca ningún archivo.")) return;
        _app.Config.Limpiezas.Remove(cleanup);
        Save();
    }

    private async Task TestCleanupAsync()
    {
        var cleanup = RequireSelected<CleanupRule>(_cleanups, "una limpieza");
        if (cleanup == null) return;
        var results = await Task.Run(() => _app.Organizer.ScanAll(simulate: true, onlyCleanup: cleanup));
        using var form = new SimulationForm(results, $"Prueba de la limpieza de {cleanup.CarpetaOrigen}", null, "Cerrar");
        form.ShowDialog(this);
    }

    // ---------- Opciones ----------

    private Control BuildOptionsTab()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string label, Control control, string? hint = null)
        {
            int row = t.RowCount++;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) }, 0, row);
            if (hint == null)
            {
                t.Controls.Add(control, 1, row);
                return;
            }
            var flow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            flow.Controls.Add(control);
            flow.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 8, 3, 3) });
            t.Controls.Add(flow, 1, row);
        }

        _interval.Items.AddRange(Intervals.Select(i => (object)$"{i} min").ToArray());
        _wait.Items.AddRange(Waits.Select(w => (object)(w == 0 ? "Sin espera" : $"{w} min")).ToArray());
        _notifications.Items.AddRange(new object[] { "Nunca", "Por cada archivo movido", "Resumen al terminar cada barrido" });
        _alertHours.Items.AddRange(AlertHours.Select(h => (object)$"{h} h").ToArray());

        Row("Barrido de respaldo cada:", _interval, "Revisa todas las carpetas por si algo se pasó por alto.");
        Row("Esperar antes de mover:", _wait, "Un archivo se mueve recién cuando pasa este tiempo sin modificarse.");
        Row("Notificaciones:", _notifications);
        Row("Alerta si Drive no responde por:", _alertHours, "Muestra un aviso grande que no se puede ignorar.");
        Row("Avisar a:", _contact, "Aparece en la alerta. Ej.: Diego (11 5555-5555)");
        Row("", _autostart);
        Row("", _reopen, "Si alguien la cierra, se vuelve a abrir en 5 minutos.");

        var pinRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        var setPin = new Button { Text = "Configurar PIN…", AutoSize = true };
        setPin.Click += (_, _) => SetPin();
        _removePin.Click += (_, _) => RemovePin();
        pinRow.Controls.Add(_pinStatus);
        pinRow.Controls.Add(setPin);
        pinRow.Controls.Add(_removePin);
        Row("PIN de administrador:", pinRow);

        var dataLink = new LinkLabel { Text = "Abrir carpeta de datos (configuración y log)", AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
        dataLink.LinkClicked += (_, _) => { Directory.CreateDirectory(AppConfig.DataDir); TrayContext.OpenInExplorer(AppConfig.DataDir); };
        Row("", dataLink);

        _interval.SelectedIndexChanged += (_, _) => SetOption(() => _app.Config.IntervaloBarridoMin = Intervals[_interval.SelectedIndex]);
        _wait.SelectedIndexChanged += (_, _) => SetOption(() => _app.Config.EsperaMinutos = Waits[_wait.SelectedIndex]);
        _notifications.SelectedIndexChanged += (_, _) => SetOption(() => _app.Config.Notificaciones = (NotificationMode)_notifications.SelectedIndex);
        _alertHours.SelectedIndexChanged += (_, _) => SetOption(() => _app.Config.AlertaHoras = AlertHours[_alertHours.SelectedIndex]);
        _contact.Leave += (_, _) =>
        {
            if (_contact.Text.Trim() == _app.Config.Contacto) return;
            SetOption(() => _app.Config.Contacto = _contact.Text.Trim());
        };
        _reopen.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _app.Config.NoReabrir = !_reopen.Checked;
            Watchdog.Apply(_reopen.Checked);
            _app.SaveConfig();
        };
        _autostart.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            try { Autostart.Set(_autostart.Checked); }
            catch (Exception ex) { FormKit.Warn(this, ex.Message); }
        };
        return t;
    }

    private void SetOption(Action apply)
    {
        if (_loading) return;
        apply();
        _app.SaveConfig();
    }

    private static int IndexOr(int[] values, int value, int fallback)
    {
        var i = Array.IndexOf(values, value);
        return i >= 0 ? i : fallback;
    }

    private void LoadOptions()
    {
        _loading = true;
        var c = _app.Config;
        _interval.SelectedIndex = IndexOr(Intervals, c.IntervaloBarridoMin, 1);
        _wait.SelectedIndex = IndexOr(Waits, c.EsperaMinutos, 2);
        _notifications.SelectedIndex = (int)c.Notificaciones;
        _alertHours.SelectedIndex = IndexOr(AlertHours, c.AlertaHoras, 2);
        if (!_contact.Focused) _contact.Text = c.Contacto;
        _autostart.Checked = Autostart.IsEnabled();
        _reopen.Checked = !c.NoReabrir;
        _pinStatus.Text = c.TienePin ? "Activo" : "Sin PIN: cualquiera puede cambiar la configuración";
        _pinStatus.ForeColor = c.TienePin ? Color.DarkGreen : Color.DarkOrange;
        _removePin.Enabled = c.TienePin;
        _loading = false;
    }

    private void SetPin()
    {
        using var dialog = new PinSetupDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        (_app.Config.PinHash, _app.Config.PinSalt) = PinHasher.Create(dialog.Pin);
        _app.KeepUnlocked();
        Save();
        MessageBox.Show(this, "PIN configurado. Anotalo en un lugar seguro: sin él no se puede cambiar la configuración.",
            "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RemovePin()
    {
        if (!FormKit.Confirm(this, "¿Quitar el PIN? Cualquiera va a poder cambiar la configuración o cerrar OrdenaPC.")) return;
        _app.Config.PinHash = "";
        _app.Config.PinSalt = "";
        Save();
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
