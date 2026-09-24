namespace OrdenaPC;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Si se instala, se lanza la copia instalada y este proceso termina.
        if (Installer.OfferInstall()) return;

        using var mutex = new Mutex(true, @"Local\OrdenaPC-SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("OrdenaPC ya se está ejecutando. Buscá el ícono junto al reloj.", "OrdenaPC",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayContext());
    }
}
