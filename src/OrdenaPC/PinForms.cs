using OrdenaPC.Core;

namespace OrdenaPC;

/// <summary>Pide el PIN; queda abierto hasta que sea correcto o se cancele.</summary>
sealed class PinDialog : Form
{
    private readonly TextBox _pin = new() { UseSystemPasswordChar = true, MaxLength = 8, Width = 120, Font = new Font("Segoe UI", 14F) };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private int _failures;

    public PinDialog(string action, Func<string, bool> verify)
    {
        var t = FormKit.Dialog(this, "OrdenaPC — PIN");
        FormKit.Row(t, "", new Label { Text = $"Para {action} ingresá el PIN de administrador.", AutoSize = true });
        FormKit.Row(t, "PIN:", _pin);
        FormKit.Row(t, "", _error);
        FormKit.OkCancel(this, t, () =>
        {
            if (verify(_pin.Text)) return true;
            // Una pausa creciente hace inútil probar PINs al azar.
            _failures++;
            Thread.Sleep(Math.Min(5000, 700 * _failures));
            _error.Text = "PIN incorrecto.";
            _pin.SelectAll();
            _pin.Focus();
            return false;
        }, okText: "Aceptar");
    }
}

/// <summary>Configura un PIN nuevo (dos veces, 4 a 8 números).</summary>
sealed class PinSetupDialog : Form
{
    private readonly TextBox _pin = new() { UseSystemPasswordChar = true, MaxLength = 8, Width = 120 };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true, MaxLength = 8, Width = 120 };

    public PinSetupDialog()
    {
        var t = FormKit.Dialog(this, "Configurar PIN");
        FormKit.Row(t, "PIN nuevo:", _pin);
        FormKit.Row(t, "Repetir PIN:", _repeat);
        FormKit.Hint(t, "De 4 a 8 números. Se va a pedir para abrir este panel, pausar, deshacer o cerrar OrdenaPC.");
        FormKit.OkCancel(this, t, () =>
        {
            if (!PinHasher.IsValidFormat(_pin.Text)) { FormKit.Warn(this, "El PIN tiene que tener de 4 a 8 números."); return false; }
            if (_pin.Text != _repeat.Text) { FormKit.Warn(this, "Los dos PIN no coinciden."); return false; }
            return true;
        });
    }

    public string Pin => _pin.Text;
}
