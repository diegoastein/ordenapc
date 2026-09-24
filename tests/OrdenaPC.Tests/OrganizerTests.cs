using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class OrganizerTests
{
    private sealed class Setup : IDisposable
    {
        public TempDir T { get; } = new();
        public string Desk { get; }
        public string Drive { get; }
        public AppConfig Config { get; } = new() { EsperaMinutos = 0 };
        public MoveLog Log { get; }
        public Organizer Org { get; }

        public Setup()
        {
            Desk = T.Dir("Escritorio");
            Drive = T.Dir("Drive");
            Config.Reglas.Add(new Rule
            {
                Nombre = "Epicrisis",
                CarpetaOrigen = Desk,
                Extensiones = new() { ".docx" },
                PalabrasClave = new() { "epicrisis" },
                CarpetaDestino = Path.Combine(Drive, "Epicrisis"),
            });
            Log = new MoveLog(Path.Combine(T.Root, "log.csv"));
            Org = new Organizer(() => Config, new SafeMover(), Log);
        }

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public void Simulacion_no_mueve_y_no_registra()
    {
        using var s = new Setup();
        var f = TempDir.CreateFile(s.Desk, "epicrisis_ana.docx");

        var results = s.Org.ScanAll(simulate: true);

        Assert.Single(results);
        Assert.Equal(MoveStatus.Simulado, results[0].Estado);
        Assert.True(File.Exists(f));
        Assert.Empty(s.Log.ReadAll());
    }

    [Fact]
    public void Barrido_mueve_lo_que_coincide_y_deja_lo_demas()
    {
        using var s = new Setup();
        TempDir.CreateFile(s.Desk, "epicrisis_ana.docx");
        var other = TempDir.CreateFile(s.Desk, "receta.docx");
        var temp = TempDir.CreateFile(s.Desk, "~$epicrisis_ana.docx");

        var results = s.Org.ScanAll(simulate: false);

        Assert.Single(results);
        Assert.True(File.Exists(Path.Combine(s.Drive, "Epicrisis", "epicrisis_ana.docx")));
        Assert.True(File.Exists(other));
        Assert.True(File.Exists(temp));
        var log = Assert.Single(s.Log.ReadAll());
        Assert.Equal(MoveStatus.Movido, log.Estado);
        Assert.Equal("Epicrisis", log.Regla);
    }

    [Fact]
    public void Pendiente_se_registra_una_vez_y_se_mueve_al_reintentar()
    {
        using var s = new Setup();
        var driveRoot = Path.Combine(s.T.Root, "G");
        s.Config.Reglas[0].CarpetaDestino = Path.Combine(driveRoot, "Mi unidad", "Epicrisis");
        var f = TempDir.CreateFile(s.Desk, "epicrisis_ana.docx");

        s.Org.ScanAll(simulate: false);
        s.Org.ScanAll(simulate: false);
        Assert.True(File.Exists(f));
        Assert.Equal(1, s.Org.Pending.Count);
        Assert.Single(s.Log.ReadAll());

        Directory.CreateDirectory(Path.Combine(driveRoot, "Mi unidad"));
        var retried = s.Org.RetryPending();

        Assert.Equal(MoveStatus.Movido, Assert.Single(retried).Estado);
        Assert.Equal(0, s.Org.Pending.Count);
        Assert.False(File.Exists(f));
    }

    [Fact]
    public void Deshacer_devuelve_el_archivo_y_no_lo_vuelve_a_mover()
    {
        using var s = new Setup();
        var f = TempDir.CreateFile(s.Desk, "epicrisis_ana.docx");
        var moved = Assert.Single(s.Org.ScanAll(simulate: false));

        var undo = s.Org.Undo(moved);

        Assert.Equal(MoveStatus.Deshecho, undo.Estado);
        Assert.True(File.Exists(f));
        Assert.Contains(f, s.Config.Excluidos, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(s.Org.ScanAll(simulate: false));
        Assert.True(File.Exists(f));
    }

    [Fact]
    public void Log_soporta_punto_y_coma_y_comillas()
    {
        using var t = new TempDir();
        var log = new MoveLog(Path.Combine(t.Root, "log.csv"));
        var r = new MoveResult(new DateTime(2026, 9, 24, 10, 0, 0), "a;b", "C:\\x \"y\".docx", "D:\\z", MoveStatus.Error, "línea1\nlínea2");

        log.Append(r);
        var back = Assert.Single(log.ReadAll());

        Assert.Equal("a;b", back.Regla);
        Assert.Equal("C:\\x \"y\".docx", back.Origen);
        Assert.Equal("línea1 línea2", back.Detalle);
    }
}
