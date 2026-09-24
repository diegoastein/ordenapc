using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class ConflictNamerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 14, 32, 10);

    [Fact]
    public void Sin_conflicto_mantiene_el_nombre()
    {
        using var t = new TempDir();
        Assert.Equal(Path.Combine(t.Root, "a.docx"), ConflictNamer.GetAvailablePath(t.Root, "a.docx", Now));
    }

    [Fact]
    public void Con_conflicto_agrega_fecha_y_luego_numero()
    {
        using var t = new TempDir();
        TempDir.CreateFile(t.Root, "epicrisis_juanperez.docx");
        var p1 = ConflictNamer.GetAvailablePath(t.Root, "epicrisis_juanperez.docx", Now);
        Assert.Equal("epicrisis_juanperez_2026-09-24_14-32.docx", Path.GetFileName(p1));

        File.WriteAllText(p1, "x");
        var p2 = ConflictNamer.GetAvailablePath(t.Root, "epicrisis_juanperez.docx", Now);
        Assert.Equal("epicrisis_juanperez_2026-09-24_14-32_2.docx", Path.GetFileName(p2));
    }
}
