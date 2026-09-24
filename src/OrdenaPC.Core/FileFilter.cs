namespace OrdenaPC.Core;

public static class FileFilter
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".crdownload", ".part", ".partial", ".download", ".opdownload",
        ".ordenapc-tmp", ".lnk", ".ini",
    };

    /// <summary>Temporales, descargas a medio bajar, ocultos y archivos de sistema no se tocan nunca.</summary>
    public static bool ShouldIgnore(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("~$") || name.StartsWith(".")) return true;
        if (IgnoredExtensions.Contains(Path.GetExtension(name))) return true;
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory)) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
