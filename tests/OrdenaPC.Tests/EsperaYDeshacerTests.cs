using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class EsperaYDeshacerTests
{
    private static (TempDir t, string desk, AppConfig config, MoveLog log, Organizer org) Setup(int waitMinutes)
    {
        var t = new TempDir();
        var desk = t.Dir("Escritorio");
        var config = new AppConfig { EsperaMinutos = waitMinutes };
        config.Reglas.Add(new Rule
        {
            Nombre = "Epicrisis",
            CarpetaOrigen = desk,
            Extensiones = { ".docx" },
            PalabrasClave = { "epicrisis" },
            CarpetaDestino = Path.Combine(t.Dir("Drive"), "Epicrisis"),
        });
        var log = new MoveLog(Path.Combine(t.Root, "log.csv"));
        return (t, desk, config, log, new Organizer(() => config, new SafeMover(), log));
    }

    private static void Age(string path, int minutes)
    {
        var when = DateTime.UtcNow.AddMinutes(-minutes);
        File.SetCreationTimeUtc(path, when);
        File.SetLastWriteTimeUtc(path, when);
    }

    [Fact]
    public void Archivo_recien_modificado_espera_y_no_se_registra()
    {
        var (t, desk, _, log, org) = Setup(10);
        using var _t = t;
        var f = TempDir.CreateFile(desk, "epicrisis_ana.docx");

        var r = Assert.Single(org.ScanAll(simulate: false));

        Assert.Equal(MoveStatus.Esperando, r.Estado);
        Assert.True(File.Exists(f));
        Assert.Empty(log.ReadAll());
        var ready = org.ReadyAtUtc(f);
        Assert.NotNull(ready);
        Assert.InRange(ready!.Value, DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public void Archivo_sin_cambios_hace_mas_que_la_espera_se_mueve()
    {
        var (t, desk, _, _, org) = Setup(10);
        using var _t = t;
        var f = TempDir.CreateFile(desk, "epicrisis_ana.docx");
        Age(f, 11);

        Assert.Null(org.ReadyAtUtc(f));
        Assert.Equal(MoveStatus.Movido, Assert.Single(org.ScanAll(simulate: false)).Estado);
        Assert.False(File.Exists(f));
    }

    [Fact]
    public void Sin_espera_se_mueve_enseguida()
    {
        var (t, desk, _, _, org) = Setup(0);
        using var _t = t;
        TempDir.CreateFile(desk, "epicrisis_ana.docx");

        Assert.Equal(MoveStatus.Movido, Assert.Single(org.ScanAll(simulate: false)).Estado);
    }

    [Fact]
    public void Simulacion_muestra_el_archivo_en_espera_como_que_se_moveria()
    {
        var (t, desk, _, _, org) = Setup(10);
        using var _t = t;
        TempDir.CreateFile(desk, "epicrisis_ana.docx");

        var r = Assert.Single(org.ScanAll(simulate: true));

        Assert.Equal(MoveStatus.Simulado, r.Estado);
        Assert.Contains("10 min", r.Detalle);
    }

    [Fact]
    public void Deshacer_ultimo_toma_el_mas_reciente_que_sigue_en_destino()
    {
        var (t, desk, _, log, org) = Setup(0);
        using var _t = t;
        TempDir.CreateFile(desk, "epicrisis_1.docx");
        org.ScanAll(simulate: false);
        TempDir.CreateFile(desk, "epicrisis_2.docx");
        org.ScanAll(simulate: false);

        var last = Organizer.LastUndoable(log.ReadAll());
        Assert.Equal("epicrisis_2.docx", Path.GetFileName(last!.Origen));

        Assert.Equal(MoveStatus.Deshecho, org.Undo(last).Estado);
        var next = Organizer.LastUndoable(log.ReadAll());
        Assert.Equal("epicrisis_1.docx", Path.GetFileName(next!.Origen));

        org.Undo(next);
        Assert.Null(Organizer.LastUndoable(log.ReadAll()));
    }
}
