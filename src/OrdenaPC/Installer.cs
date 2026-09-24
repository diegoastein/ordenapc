using System.Diagnostics;

namespace OrdenaPC;

/// <summary>Instalación sin administrador: copia el .exe a %LOCALAPPDATA%\OrdenaPC y lo registra para arrancar con Windows.</summary>
static class Installer
{
    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrdenaPC");

    public static string InstalledExe => Path.Combine(InstallDir, "OrdenaPC.exe");

    /// <summary>Devuelve true si este proceso tiene que terminar (se lanzó la copia instalada o falló la copia).</summary>
    public static bool OfferInstall()
    {
        var current = Environment.ProcessPath;
        if (current == null ||
            string.Equals(Path.GetFullPath(current), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
            return false;

        var message = File.Exists(InstalledExe)
            ? "Ya hay una versión de OrdenaPC instalada. ¿Reemplazarla por esta?"
            : "¿Instalar OrdenaPC en tu usuario y hacer que arranque con Windows?\n\n" +
              "No necesita permisos de administrador. Si elegís No, se ejecuta desde acá sin instalar.";
        if (MessageBox.Show(message, "OrdenaPC", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;

        try
        {
            Directory.CreateDirectory(InstallDir);
            File.Copy(current, InstalledExe, overwrite: true);
        }
        catch (IOException)
        {
            MessageBox.Show("No se pudo copiar porque OrdenaPC ya está abierto.\n\n" +
                "Cerralo desde el ícono junto al reloj (Salir) y volvé a abrir este archivo.",
                "OrdenaPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
        }

        Autostart.Set(true, InstalledExe);
        Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
        return true;
    }
}
