using OrdenaPC.Core;

namespace OrdenaPC.Tests;

public class RuleMatcherTests
{
    private static Rule R(string exts, string kws, string origin = "") => new()
    {
        CarpetaOrigen = origin,
        Extensiones = TextUtil.SplitList(exts),
        PalabrasClave = TextUtil.SplitList(kws),
        CarpetaDestino = "x",
    };

    [Theory]
    [InlineData("epicrisis_juanperez.docx", true)]
    [InlineData("Epicrísis Juan.DOCX", true)]
    [InlineData("EPICRISIS.docx", true)]
    [InlineData("epicrisis_juanperez.pdf", false)]
    [InlineData("informe.docx", false)]
    public void Coincide_por_extension_y_palabra_sin_acentos_ni_mayusculas(string file, bool expected)
    {
        Assert.Equal(expected, RuleMatcher.Matches(R(".docx", "epicrisis"), file));
    }

    [Fact]
    public void Palabra_clave_con_acento_coincide_con_nombre_sin_acento()
    {
        Assert.True(RuleMatcher.Matches(R("pdf", "Epicrísis"), "epicrisis_2026.pdf"));
    }

    [Fact]
    public void Varias_palabras_alcanza_con_una()
    {
        var rule = R(".pdf, .docx", "informe, epicrisis");
        Assert.True(RuleMatcher.Matches(rule, "INFORME_final.pdf"));
        Assert.True(RuleMatcher.Matches(rule, "epicrisis.docx"));
        Assert.False(RuleMatcher.Matches(rule, "factura.pdf"));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("*.docx")]
    [InlineData(".DOCX")]
    public void Normaliza_formatos_de_extension(string ext)
    {
        Assert.True(RuleMatcher.Matches(R(ext, ""), "cualquiera.docx"));
    }

    [Fact]
    public void Sin_extensiones_nunca_coincide()
    {
        Assert.False(RuleMatcher.Matches(R("", "epicrisis"), "epicrisis.docx"));
    }

    [Fact]
    public void FindRule_respeta_carpeta_origen_y_orden()
    {
        using var t = new TempDir();
        var desk = t.Dir("Escritorio");
        var down = t.Dir("Descargas");
        var first = R(".pdf", "informe", desk);
        var second = R(".pdf", "", desk);
        var other = R(".pdf", "", down);
        var rules = new[] { other, first, second };

        Assert.Same(first, RuleMatcher.FindRule(rules, Path.Combine(desk, "informe.pdf")));
        Assert.Same(second, RuleMatcher.FindRule(rules, Path.Combine(desk, "otro.pdf")));
        Assert.Same(other, RuleMatcher.FindRule(rules, Path.Combine(down, "informe.pdf")));
        Assert.Null(RuleMatcher.FindRule(rules, Path.Combine(t.Root, "informe.pdf")));
    }
}
