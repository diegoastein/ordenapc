namespace OrdenaPC.Core;

public static class RuleMatcher
{
    /// <summary>
    /// El archivo coincide si su extensión está en la lista y su nombre contiene alguna
    /// de las palabras clave (sin distinguir mayúsculas ni acentos). Una regla sin
    /// extensiones nunca coincide; una sin palabras clave acepta cualquier nombre.
    /// </summary>
    public static bool Matches(Rule rule, string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (rule.Extensiones.Count == 0 || !rule.Extensiones.Any(e => TextUtil.NormalizeExtension(e) == ext))
            return false;

        if (rule.PalabrasClave.Count == 0) return true;

        var name = TextUtil.Normalize(Path.GetFileNameWithoutExtension(filePath));
        return rule.PalabrasClave.Any(k =>
            !string.IsNullOrWhiteSpace(k) && name.IndexOf(TextUtil.Normalize(k.Trim()), StringComparison.Ordinal) >= 0);
    }

    public static bool IsInOrigin(Rule rule, string filePath) => IsInFolder(rule.CarpetaOrigen, filePath);

    /// <summary>El archivo está directamente en esa carpeta (no en una subcarpeta).</summary>
    public static bool IsInFolder(string folder, string filePath)
    {
        var normalized = TextUtil.TryNormalizeFolder(folder);
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        return normalized.Length > 0 && dir != null
            && string.Equals(TextUtil.NormalizeFolder(dir), normalized, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Primera regla (en el orden de la tabla) que aplica al archivo, o null.</summary>
    public static Rule? FindRule(IEnumerable<Rule> rules, string filePath) =>
        rules.FirstOrDefault(r => IsInOrigin(r, filePath) && Matches(r, filePath));
}
