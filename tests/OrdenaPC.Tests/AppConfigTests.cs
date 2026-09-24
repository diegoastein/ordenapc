using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class AppConfigTests
{
    [Fact]
    public void Guarda_y_carga_la_configuracion()
    {
        using var t = new TempDir();
        var path = Path.Combine(t.Root, "config.json");
        var config = new AppConfig
        {
            IntervaloBarridoMin = 60,
            Notificaciones = NotificationMode.CadaArchivo,
            PrimerBarridoHecho = true,
            EsperaMinutos = 10,
            Excluidos = { @"C:\x\epicrisis.docx" },
        };
        config.Reglas.Add(new Rule
        {
            Nombre = "Epicrisis",
            CarpetaOrigen = @"C:\Users\ana\Desktop",
            Extensiones = { ".docx", ".pdf" },
            PalabrasClave = { "epicrisis", "alta médica" },
            CarpetaDestino = @"G:\Mi unidad\Epicrisis",
            Activa = false,
        });

        config.Save(path);
        config.Save(path); // la segunda vez reemplaza el archivo existente
        var back = AppConfig.Load(path);

        Assert.Equal(60, back.IntervaloBarridoMin);
        Assert.Equal(NotificationMode.CadaArchivo, back.Notificaciones);
        Assert.True(back.PrimerBarridoHecho);
        Assert.Equal(10, back.EsperaMinutos);
        Assert.Equal(config.Excluidos, back.Excluidos);
        var r = Assert.Single(back.Reglas);
        Assert.Equal(config.Reglas[0].Id, r.Id);
        Assert.Equal("Epicrisis", r.Nombre);
        Assert.Equal(new[] { ".docx", ".pdf" }, r.Extensiones);
        Assert.Equal(new[] { "epicrisis", "alta médica" }, r.PalabrasClave);
        Assert.Equal(@"G:\Mi unidad\Epicrisis", r.CarpetaDestino);
        Assert.False(r.Activa);
    }

    [Fact]
    public void Json_incompleto_carga_con_valores_por_defecto()
    {
        using var t = new TempDir();
        var path = Path.Combine(t.Root, "config.json");
        File.WriteAllText(path, "{\"Pausado\":true,\"Reglas\":[{\"Nombre\":\"x\"}]}");

        var c = AppConfig.Load(path);

        Assert.True(c.Pausado);
        Assert.NotNull(c.Excluidos);
        Assert.Equal(30, c.IntervaloBarridoMin);
        Assert.NotNull(Assert.Single(c.Reglas).Extensiones);
    }

    [Fact]
    public void Json_danado_no_rompe_y_guarda_copia()
    {
        using var t = new TempDir();
        var path = Path.Combine(t.Root, "config.json");
        File.WriteAllText(path, "{ esto no es json");

        var c = AppConfig.Load(path);

        Assert.Empty(c.Reglas);
        Assert.True(File.Exists(path + ".corrupto"));
    }
}
