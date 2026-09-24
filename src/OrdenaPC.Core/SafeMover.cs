namespace OrdenaPC.Core;

/// <summary>
/// Mueve un archivo sin arriesgarlo: si el destino no está disponible no hace nada,
/// si ya existe un archivo con ese nombre usa uno nuevo, y entre discos distintos
/// copia, verifica y recién después quita el original.
/// </summary>
public sealed class SafeMover
{
    private readonly Func<DateTime> _now;

    public SafeMover(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    /// <summary>Para tests: usa copiar-verificar-borrar aunque origen y destino estén en el mismo disco.</summary>
    public bool ForceCopyMode { get; set; }

    public MoveResult Move(string source, string destDir, string ruleName, bool simulate, string? targetName = null)
    {
        var now = _now();
        MoveResult Result(string dest, MoveStatus status, string detail = "") =>
            new(now, ruleName, source, dest, status, detail);

        if (!File.Exists(source))
            return Result("", MoveStatus.Error, "El archivo ya no existe en el origen.");

        var fileName = targetName ?? Path.GetFileName(source);

        string dir;
        try { dir = TextUtil.NormalizeFolder(destDir); }
        catch (Exception ex) { return Result(destDir, MoveStatus.Error, "Ruta de destino inválida: " + ex.Message); }
        if (dir.Length == 0)
            return Result("", MoveStatus.Error, "La regla no tiene carpeta destino.");

        // La subcarpeta final se puede crear, pero solo si su carpeta padre ya existe:
        // si falta la unidad o la raíz de Google Drive, no se inventa nada y se reintenta después.
        if (!Directory.Exists(dir))
        {
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || !Directory.Exists(parent))
                return Result(Path.Combine(dir, fileName), MoveStatus.Pendiente,
                    "La carpeta destino no está disponible (¿Google Drive desconectado?).");
            if (!simulate)
            {
                try { Directory.CreateDirectory(dir); }
                catch (Exception ex)
                {
                    return Result(Path.Combine(dir, fileName), MoveStatus.Pendiente,
                        "No se pudo crear la carpeta destino: " + ex.Message);
                }
            }
        }

        var target = Directory.Exists(dir)
            ? ConflictNamer.GetAvailablePath(dir, fileName, now)
            : Path.Combine(dir, fileName);
        var renamed = Path.GetFileName(target) != fileName ? "Renombrado por conflicto de nombre." : "";

        if (simulate) return Result(target, MoveStatus.Simulado, renamed);

        if (IsLocked(source))
            return Result(target, MoveStatus.EnUso, "El archivo está abierto en otro programa.");

        try
        {
            if (!ForceCopyMode && SameVolume(source, dir))
            {
                File.Move(source, target, overwrite: false);
            }
            else
            {
                var error = CopyVerifyDelete(source, target);
                if (error != null) return Result(target, MoveStatus.Error, error);
            }
        }
        catch (UnauthorizedAccessException ex) { return Result(target, MoveStatus.Error, ex.Message); }
        catch (FileNotFoundException ex) { return Result(target, MoveStatus.Error, ex.Message); }
        catch (IOException ex) { return Result(target, MoveStatus.Pendiente, ex.Message); }

        return Result(target, MoveStatus.Movido, renamed);
    }

    /// <summary>Devuelve null si salió bien, o el motivo si hubo que revertir.</summary>
    private static string? CopyVerifyDelete(string source, string target)
    {
        var tmp = target + ".ordenapc-tmp";
        try
        {
            File.Copy(source, tmp, overwrite: true);
            if (new FileInfo(source).Length != new FileInfo(tmp).Length)
                throw new IOException("La copia no coincide en tamaño con el original.");
            File.Move(tmp, target, overwrite: false);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }

        try
        {
            File.Delete(source);
        }
        catch (Exception ex)
        {
            // Si el original no se puede quitar, se descarta la copia para no dejar el archivo duplicado.
            TryDelete(target);
            return "Se copió pero no se pudo quitar el original; se revirtió la copia: " + ex.Message;
        }
        return null;
    }

    public static bool IsLocked(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool SameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) { }
    }
}
