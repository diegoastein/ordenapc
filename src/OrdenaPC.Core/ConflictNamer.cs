using System.Globalization;

namespace OrdenaPC.Core;

public static class ConflictNamer
{
    /// <summary>
    /// Devuelve una ruta libre en destDir: el nombre original si no existe, si no
    /// nombre_2026-09-24_14-32.ext, y si también existe, nombre_2026-09-24_14-32_2.ext, etc.
    /// </summary>
    public static string GetAvailablePath(string destDir, string fileName, DateTime now)
    {
        var path = Path.Combine(destDir, fileName);
        if (!Exists(path)) return path;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var stamp = now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);

        path = Path.Combine(destDir, $"{baseName}_{stamp}{ext}");
        for (int i = 2; Exists(path); i++)
            path = Path.Combine(destDir, $"{baseName}_{stamp}_{i}{ext}");
        return path;
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
