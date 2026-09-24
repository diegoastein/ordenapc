using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>
/// Buzón en el escritorio: un recuadro de color sin bordes donde se sueltan archivos.
/// Queda "pegado" al escritorio (dueño = Progman), así Win+D no lo esconde.
/// </summary>
sealed class MailboxForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WM_NCHITTEST = 0x84;
    private const int HTCAPTION = 2;
    private const int HTBOTTOMRIGHT = 17;
    private const int GWL_HWNDPARENT = -8;
    private const int CornerRadius = 14;
    private const int Pad = 10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    private readonly TrayContext _app;
    private readonly System.Windows.Forms.Timer _flashTimer = new();
    private string? _flash;
    private bool _flashWarning;
    private bool _dragOver;
    private bool _editMode;
    private int _todayCount;
    private Rectangle _openButton;

    public MailboxForm(TrayContext app, Mailbox box)
    {
        _app = app;
        Box = box;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AllowDrop = true;
        DoubleBuffered = true;
        MinimumSize = new Size(120, 80);
        _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); _flash = null; Invalidate(); };
        ApplyBox(box, keepBounds: false);
    }

    public Mailbox Box { get; private set; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // fuera de Alt+Tab
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return;
        if (IntPtr.Size == 8) SetWindowLongPtr64(Handle, GWL_HWNDPARENT, progman);
        else SetWindowLong32(Handle, GWL_HWNDPARENT, progman.ToInt32());
    }

    public void ApplyBox(Mailbox box, bool keepBounds)
    {
        Box = box;
        Text = box.Nombre;
        if (!keepBounds) Bounds = new Rectangle(box.X, box.Y, box.Ancho, box.Alto);
        UpdateShape();
        Invalidate();
    }

    public void SetEditMode(bool on)
    {
        _editMode = on;
        Invalidate();
    }

    public void SetTodayCount(int count)
    {
        if (count == _todayCount) return;
        _todayCount = count;
        Invalidate();
    }

    public void Flash(string text, bool warning, bool sticky = false)
    {
        _flash = text;
        _flashWarning = warning;
        _flashTimer.Stop();
        if (!sticky)
        {
            _flashTimer.Interval = warning ? 9000 : 4000;
            _flashTimer.Start();
        }
        Invalidate();
    }

    // ---------- Dibujo ----------

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateShape();
        Invalidate();
    }

    private void UpdateShape()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var baseColor = Color.FromArgb(255, Color.FromArgb(Box.Color));
        var background = _dragOver ? ControlPaint.Light(baseColor, 0.4f) : baseColor;
        var fore = Luminance(baseColor) > 0.6 ? Color.FromArgb(30, 30, 30) : Color.White;
        using (var b = new SolidBrush(background)) g.FillRectangle(b, ClientRectangle);

        if (_dragOver || _editMode)
        {
            using var pen = new Pen(fore, 3) { DashStyle = _editMode ? DashStyle.Dash : DashStyle.Solid };
            using var border = RoundedRect(Rectangle.Inflate(ClientRectangle, -3, -3), CornerRadius - 2);
            g.DrawPath(pen, border);
        }

        int titlePx = Clamp(Height / 7, 14, 40);
        int bodyPx = Clamp(Height / 12, 11, 22);
        using var titleFont = new Font("Segoe UI", titlePx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var bodyFont = new Font("Segoe UI", bodyPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);

        var titleRect = new Rectangle(Pad + 2, Pad, Width - 2 * Pad - 4, (int)(titlePx * 1.35));
        TextRenderer.DrawText(g, Box.Nombre, titleFont, titleRect, fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Botón "Abrir carpeta" abajo a la derecha, contador abajo a la izquierda.
        const string openText = "Abrir carpeta ›";
        var textSize = TextRenderer.MeasureText(openText, smallFont);
        _openButton = new Rectangle(Width - Pad - textSize.Width - 14, Height - Pad - textSize.Height - 6,
            textSize.Width + 14, textSize.Height + 6);
        if (!_editMode)
        {
            using (var fill = new SolidBrush(Color.FromArgb(45, fore)))
            using (var path = RoundedRect(_openButton, 6))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, openText, smallFont, _openButton, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (_todayCount > 0)
            {
                var countRect = new Rectangle(Pad + 2, _openButton.Top, _openButton.Left - Pad - 6, _openButton.Height);
                TextRenderer.DrawText(g, $"{_todayCount} hoy", smallFont, countRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        var middle = Rectangle.FromLTRB(Pad, titleRect.Bottom + 2, Width - Pad, _openButton.Top - 4);
        string message = _editMode ? "Arrastrá para mover.\nEsquina de abajo a la derecha: tamaño."
            : _flash ?? (_dragOver ? "Soltá para archivar" : "Arrastrá acá los archivos");
        if (_flash != null && _flashWarning && !_editMode)
        {
            using var band = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            using var path = RoundedRect(middle, 8);
            g.FillPath(band, path);
            fore = Color.White;
        }
        TextRenderer.DrawText(g, message, bodyFont, Rectangle.Inflate(middle, -4, -2), fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // ---------- Mouse: abrir carpeta y modo acomodar ----------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = !_editMode && _openButton.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_editMode || e.Button != MouseButtons.Left || !_openButton.Contains(e.Location)) return;
        var dir = TextUtil.TryNormalizeFolder(Box.CarpetaDestino);
        if (dir.Length > 0 && Directory.Exists(dir))
        {
            try { Process.Start("explorer.exe", $"\"{dir}\""); }
            catch (Exception) { Flash("No se pudo abrir la carpeta.", true); }
        }
        else
        {
            Flash("La carpeta no está disponible ahora (¿Google Drive cerrado?).", true);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST || !_editMode) return;
        long lp = m.LParam.ToInt64();
        var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
        m.Result = (IntPtr)(p.X >= Width - 20 && p.Y >= Height - 20 ? HTBOTTOMRIGHT : HTCAPTION);
    }

    // ---------- Arrastrar y soltar ----------

    private bool Accepts(IDataObject? data) =>
        !_editMode && data != null &&
        (data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("FileGroupDescriptorW"));

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        // Copy (no Move): así el Explorador nunca borra nada por su cuenta; el movimiento lo hace OrdenaPC.
        e.Effect = Accepts(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        _dragOver = e.Effect != DragDropEffects.None;
        Invalidate();
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effect = Accepts(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _dragOver = false;
        Invalidate();
    }

    protected override async void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        _dragOver = false;
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            // Adjuntos de correo o del navegador: no son archivos todavía.
            Flash("Primero guardá el archivo en el Escritorio y después arrastralo acá.", true);
            return;
        }

        Flash("Archivando…", false, sticky: true);
        var box = Box;
        IReadOnlyList<MoveResult> results;
        try
        {
            results = await Task.Run(() => _app.Organizer.DropIntoMailbox(box, paths));
        }
        catch (Exception ex)
        {
            Flash("No se pudo archivar: " + ex.Message, true);
            return;
        }
        _app.OnMailboxDrop(results);
        Flash(Describe(results), results.Any(r => r.Estado != MoveStatus.Movido));
    }

    private static string Describe(IReadOnlyList<MoveResult> results)
    {
        if (results.Count == 0) return "Ese archivo ya está en la carpeta del buzón.";
        int moved = results.Count(r => r.Estado == MoveStatus.Movido);
        int pending = results.Count(r => r.Estado == MoveStatus.Pendiente);
        var inUse = results.FirstOrDefault(r => r.Estado == MoveStatus.EnUso);
        var error = results.FirstOrDefault(r => r.Estado == MoveStatus.Error);

        var parts = new List<string>();
        if (moved > 0) parts.Add(moved == 1 ? "✓ Archivado" : $"✓ {moved} archivos archivados");
        if (pending > 0) parts.Add($"{pending} guardado(s) en la PC: se envían solos cuando la carpeta esté disponible.");
        if (inUse != null) parts.Add($"\"{Path.GetFileName(inUse.Origen)}\" está abierto. Cerralo y volvé a arrastrarlo.");
        if (error != null) parts.Add(error.Detalle);
        return string.Join("\n", parts);
    }

    // ---------- Utilidades ----------

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;

    private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

    protected override void Dispose(bool disposing)
    {
        if (disposing) _flashTimer.Dispose();
        base.Dispose(disposing);
    }
}
