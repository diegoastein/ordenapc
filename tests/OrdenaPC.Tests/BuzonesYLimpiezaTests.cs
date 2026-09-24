using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class BuzonesYLimpiezaTests : IDisposable
{
    private readonly TempDir _t = new();
    private readonly string _desk;
    private readonly string _drive;
    private readonly AppConfig _config = new() { EsperaMinutos = 0 };
    private readonly MoveLog _log;
    private readonly Organizer _org;

    public BuzonesYLimpiezaTests()
    {
        _desk = _t.Dir("Escritorio");
        _drive = _t.Dir("Drive");
        _log = new MoveLog(Path.Combine(_t.Root, "log.csv"));
        _org = new Organizer(() => _config, new SafeMover(), _log)
        {
            StagingRoot = Path.Combine(_t.Root, "staging"),
            UndoFallbackDir = _desk,
        };
    }

    public void Dispose() => _t.Dispose();

    private static void Age(string path, int days)
    {
        var when = DateTime.UtcNow.AddDays(-days);
        File.SetCreationTimeUtc(path, when);
        File.SetLastWriteTimeUtc(path, when);
    }

    private Mailbox AddMailbox(string dest)
    {
        var box = new Mailbox { Nombre = "Epicrisis", CarpetaDestino = dest };
        _config.Buzones.Add(box);
        return box;
    }

    [Fact]
    public void Buzon_mueve_el_archivo_al_destino_y_lo_registra()
    {
        var box = AddMailbox(Path.Combine(_drive, "Epicrisis"));
        var f = TempDir.CreateFile(_desk, "Documento1.docx", "hola");

        var r = Assert.Single(_org.DropIntoMailbox(box, new[] { f }));

        Assert.Equal(MoveStatus.Movido, r.Estado);
        Assert.False(File.Exists(f));
        Assert.Equal("hola", File.ReadAllText(Path.Combine(_drive, "Epicrisis", "Documento1.docx")));
        Assert.Equal("Buzón: Epicrisis", Assert.Single(_log.ReadAll()).Regla);
    }

    [Fact]
    public void Buzon_sin_destino_guarda_en_la_PC_y_envia_al_volver()
    {
        var driveRoot = Path.Combine(_t.Root, "G");
        var box = AddMailbox(Path.Combine(driveRoot, "Mi unidad", "Epicrisis"));
        var f = TempDir.CreateFile(_desk, "Documento1.docx");

        var r = Assert.Single(_org.DropIntoMailbox(box, new[] { f }));

        Assert.Equal(MoveStatus.Pendiente, r.Estado);
        Assert.False(File.Exists(f));
        Assert.Equal(1, _org.MailboxPendingCount());

        Directory.CreateDirectory(Path.Combine(driveRoot, "Mi unidad"));
        var sent = Assert.Single(_org.RetryMailboxes());

        Assert.Equal(MoveStatus.Movido, sent.Estado);
        Assert.Equal(0, _org.MailboxPendingCount());
        Assert.True(File.Exists(Path.Combine(driveRoot, "Mi unidad", "Epicrisis", "Documento1.docx")));
    }

    [Fact]
    public void Deshacer_un_envio_diferido_devuelve_al_escritorio()
    {
        var driveRoot = Path.Combine(_t.Root, "G");
        var box = AddMailbox(Path.Combine(driveRoot, "Epicrisis"));
        TempDir.CreateFile(_desk, "Documento1.docx");
        _org.DropIntoMailbox(box, new[] { Path.Combine(_desk, "Documento1.docx") });
        Directory.CreateDirectory(driveRoot);
        var sent = Assert.Single(_org.RetryMailboxes());

        var undo = _org.Undo(sent);

        Assert.Equal(MoveStatus.Deshecho, undo.Estado);
        Assert.True(File.Exists(Path.Combine(_desk, "Documento1.docx")));
    }

    [Fact]
    public void Buzon_rechaza_carpetas()
    {
        var box = AddMailbox(Path.Combine(_drive, "Epicrisis"));
        var folder = _t.Dir("UnaCarpeta");

        var r = Assert.Single(_org.DropIntoMailbox(box, new[] { folder }));

        Assert.Equal(MoveStatus.Error, r.Estado);
        Assert.True(Directory.Exists(folder));
    }

    private CleanupRule AddCleanup(bool porMes = true) 
    {
        var c = new CleanupRule
        {
            CarpetaOrigen = _desk, Dias = 30, SubcarpetaPorMes = porMes,
            CarpetaDestino = Path.Combine(_drive, "Sin clasificar"),
        };
        _config.Limpiezas.Add(c);
        return c;
    }

    [Fact]
    public void Limpieza_mueve_solo_lo_viejo_y_sin_regla()
    {
        AddCleanup();
        _config.Reglas.Add(new Rule
        {
            CarpetaOrigen = _desk, Extensiones = { ".docx" }, PalabrasClave = { "epicrisis" },
            CarpetaDestino = Path.Combine(_drive, "Epicrisis"),
        });
        var viejo = TempDir.CreateFile(_desk, "foto.jpg");
        Age(viejo, 40);
        var nuevo = TempDir.CreateFile(_desk, "nuevo.jpg");
        var conRegla = TempDir.CreateFile(_desk, "epicrisis_ana.docx");
        Age(conRegla, 40);

        _org.ScanAll(simulate: false);

        var month = DateTime.UtcNow.AddDays(-40).ToLocalTime().ToString("yyyy-MM");
        Assert.True(File.Exists(Path.Combine(_drive, "Sin clasificar", month, "foto.jpg")));
        Assert.True(File.Exists(nuevo));
        Assert.True(File.Exists(Path.Combine(_drive, "Epicrisis", "epicrisis_ana.docx")));
    }

    [Fact]
    public void Limpieza_sin_subcarpeta_por_mes()
    {
        AddCleanup(porMes: false);
        var viejo = TempDir.CreateFile(_desk, "foto.jpg");
        Age(viejo, 40);

        _org.ScanAll(simulate: false);

        Assert.True(File.Exists(Path.Combine(_drive, "Sin clasificar", "foto.jpg")));
    }

    [Fact]
    public void Probar_limpieza_no_mueve_nada()
    {
        var c = AddCleanup();
        c.Activa = false;
        var viejo = TempDir.CreateFile(_desk, "foto.jpg");
        Age(viejo, 40);

        Assert.Empty(_org.ScanAll(simulate: false));
        var r = Assert.Single(_org.ScanAll(simulate: true, onlyCleanup: c));

        Assert.Equal(MoveStatus.Simulado, r.Estado);
        Assert.True(File.Exists(viejo));
    }

    [Fact]
    public void Pin_se_verifica_y_no_se_guarda_en_texto_plano()
    {
        var (hash, salt) = PinHasher.Create("1234");

        Assert.DoesNotContain("1234", hash);
        Assert.True(PinHasher.Verify("1234", hash, salt));
        Assert.False(PinHasher.Verify("1235", hash, salt));
        Assert.False(PinHasher.Verify("1234", "basura", salt));
        Assert.True(PinHasher.IsValidFormat("0000"));
        Assert.False(PinHasher.IsValidFormat("12a4"));
        Assert.False(PinHasher.IsValidFormat("123"));
    }

    [Fact]
    public void Monitor_recuerda_desde_cuando_falta_un_destino()
    {
        var monitor = new DestinationMonitor();
        var t0 = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);
        bool up = false;

        var down1 = monitor.Check(new[] { @"C:\g\x" }, t0, _ => up);
        var down2 = monitor.Check(new[] { @"C:\g\x" }, t0.AddHours(2), _ => up);
        Assert.Equal(t0, Assert.Single(down2).Value);

        up = true;
        Assert.Empty(monitor.Check(new[] { @"C:\g\x" }, t0.AddHours(3), _ => up));
        Assert.Single(down1);
    }
}
