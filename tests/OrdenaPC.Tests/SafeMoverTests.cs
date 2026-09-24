using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class SafeMoverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mueve_el_archivo_con_su_contenido(bool copyMode)
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.pdf", "hola");
        var dest = t.Dir("destino");

        var r = new SafeMover { ForceCopyMode = copyMode }.Move(src, dest, "r", simulate: false);

        Assert.Equal(MoveStatus.Movido, r.Estado);
        Assert.False(File.Exists(src));
        Assert.Equal("hola", File.ReadAllText(Path.Combine(dest, "a.pdf")));
        Assert.Empty(Directory.GetFiles(dest, "*.ordenapc-tmp"));
    }

    [Fact]
    public void Destino_no_disponible_queda_pendiente_y_no_toca_el_original()
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.pdf");
        var dest = Path.Combine(t.Root, "DriveDesconectado", "Epicrisis");

        var r = new SafeMover().Move(src, dest, "r", simulate: false);

        Assert.Equal(MoveStatus.Pendiente, r.Estado);
        Assert.True(File.Exists(src));
        Assert.False(Directory.Exists(dest));
    }

    [Fact]
    public void Crea_la_subcarpeta_si_existe_la_carpeta_padre()
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.pdf");
        var dest = Path.Combine(t.Dir("Drive"), "Nueva");

        var r = new SafeMover().Move(src, dest, "r", simulate: false);

        Assert.Equal(MoveStatus.Movido, r.Estado);
        Assert.True(File.Exists(Path.Combine(dest, "a.pdf")));
    }

    [Fact]
    public void Simular_no_toca_nada()
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.pdf");
        var dest = Path.Combine(t.Dir("Drive"), "Nueva");

        var r = new SafeMover().Move(src, dest, "r", simulate: true);

        Assert.Equal(MoveStatus.Simulado, r.Estado);
        Assert.True(File.Exists(src));
        Assert.False(Directory.Exists(dest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conflicto_de_nombre_no_pisa_el_existente(bool copyMode)
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.pdf", "nuevo");
        var dest = t.Dir("destino");
        TempDir.CreateFile(dest, "a.pdf", "viejo");
        var mover = new SafeMover(() => new DateTime(2026, 9, 24, 14, 32, 0)) { ForceCopyMode = copyMode };

        var r = mover.Move(src, dest, "r", simulate: false);

        Assert.Equal(MoveStatus.Movido, r.Estado);
        Assert.Equal("viejo", File.ReadAllText(Path.Combine(dest, "a.pdf")));
        Assert.Equal("nuevo", File.ReadAllText(Path.Combine(dest, "a_2026-09-24_14-32.pdf")));
    }

    [Fact]
    public void Archivo_abierto_queda_en_uso()
    {
        using var t = new TempDir();
        var src = TempDir.CreateFile(t.Dir("origen"), "a.docx");
        var dest = t.Dir("destino");

        using (new FileStream(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var r = new SafeMover().Move(src, dest, "r", simulate: false);
            Assert.Equal(MoveStatus.EnUso, r.Estado);
        }
        Assert.True(File.Exists(src));
    }
}
