using OrdenaPC.Core;

namespace OrdenaPC;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // --background: la abrió la tarea de reapertura. --autostart: la abrió Windows al iniciar.
        bool background = args.Contains("--background");
        bool silent = background || args.Contains("--autostart");

        // Si se instala, se lanza la copia instalada y este proceso termina.
        if (!silent && Installer.OfferInstall()) return;

        using var mutex = new Mutex(true, @"Local\OrdenaPC-SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            if (!silent)
                MessageBox.Show("OrdenaPC ya se está ejecutando. Buscá el ícono junto al reloj.", "OrdenaPC",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Si se cerró a propósito con "Salir", la tarea de reapertura no la vuelve a abrir.
        if (background && AppConfig.Load(TrayContext.DefaultConfigPath).Detenido) return;

        Application.Run(new TrayContext(userLaunch: !background));
    }
}
