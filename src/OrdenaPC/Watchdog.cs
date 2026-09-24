using System.Diagnostics;

namespace OrdenaPC;

/// <summary>
/// Tarea programada del usuario (no requiere administrador) que cada 5 minutos abre OrdenaPC
/// con --background. Si ya está abierta, o se cerró a propósito con "Salir", no hace nada.
/// </summary>
static class Watchdog
{
    private const string TaskName = "OrdenaPC-Vigilancia";

    public static void Apply(bool enabled)
    {
        var exe = Application.ExecutablePath;
        Task.Run(() =>
        {
            if (enabled)
                RunSchtasks($"/Create /F /SC MINUTE /MO 5 /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --background\"");
            else
                RunSchtasks($"/Delete /F /TN \"{TaskName}\"");
        });
    }

    private static void RunSchtasks(string arguments)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            p?.WaitForExit(15000);
        }
        catch (Exception)
        {
            // Si no se puede crear la tarea, la app sigue funcionando igual (solo sin reapertura automática).
        }
    }
}
