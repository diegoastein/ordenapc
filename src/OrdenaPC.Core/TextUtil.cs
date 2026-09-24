using System.Globalization;
using System.Text;

namespace OrdenaPC.Core;

public static class TextUtil
{
    /// <summary>Minúsculas y sin acentos, para que "Epicrísis" y "epicrisis" coincidan.</summary>
    public static string Normalize(string s)
    {
        var decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Separa "a, b; c" en una lista sin vacíos ni repetidos.</summary>
    public static List<string> SplitList(string? text) =>
        (text ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>"docx", "*.DOCX" y ".docx" quedan todos como ".docx".</summary>
    public static string NormalizeExtension(string ext)
    {
        var e = ext.Trim().ToLowerInvariant();
        if (e.StartsWith('*')) e = e[1..];
        return e.StartsWith('.') ? e : "." + e;
    }

    /// <summary>Ruta absoluta, con variables de entorno expandidas y sin barra final.</summary>
    public static string NormalizeFolder(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded.Length == 0) return "";
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded));
    }

    /// <summary>Igual que NormalizeFolder pero devuelve "" si la ruta es inválida.</summary>
    public static string TryNormalizeFolder(string path)
    {
        try { return NormalizeFolder(path); }
        catch (Exception) { return ""; }
    }
}
