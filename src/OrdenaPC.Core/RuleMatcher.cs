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
            !string.IsNullOrWhiteSpace(k) && name.Contains(TextUtil.Normalize(k.Trim()), StringComparison.Ordinal));
    }

    public static bool IsInOrigin(Rule rule, string filePath)
    {
        var origin = TextUtil.TryNormalizeFolder(rule.CarpetaOrigen);
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        return origin.Length > 0 && dir != null
            && string.Equals(TextUtil.NormalizeFolder(dir), origin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Primera regla (en el orden de la tabla) que aplica al archivo, o null.</summary>
    public static Rule? FindRule(IEnumerable<Rule> rules, string filePath) =>
        rules.FirstOrDefault(r => IsInOrigin(r, filePath) && Matches(r, filePath));
}
