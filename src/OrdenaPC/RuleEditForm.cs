using OrdenaPC.Core;

namespace OrdenaPC;

sealed class RuleEditForm : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly TextBox _origin = new() { Dock = DockStyle.Fill };
    private readonly TextBox _extensions = new() { Dock = DockStyle.Fill };
    private readonly TextBox _keywords = new() { Dock = DockStyle.Fill };
    private readonly TextBox _dest = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _active = new() { Text = "Regla activa", AutoSize = true };

    public RuleEditForm(Rule rule)
    {
        Result = rule;
        var t = FormKit.Dialog(this, string.IsNullOrEmpty(rule.CarpetaOrigen) ? "Nueva regla" : "Editar regla");

        FormKit.Row(t, "Nombre (opcional):", _name);
        FormKit.Row(t, "Carpeta origen:", _origin, FormKit.Browse(_origin, "Elegí la carpeta a vigilar"));
        FormKit.Row(t, "", FormKit.QuickFolders(_origin));
        FormKit.Row(t, "Extensiones:", _extensions);
        FormKit.Hint(t, "Separadas por coma. Ej.: .docx, .pdf");
        FormKit.Row(t, "Palabras clave:", _keywords);
        FormKit.Hint(t, "El nombre del archivo tiene que contener alguna. Sin distinguir mayúsculas ni acentos. Ej.: epicrisis, alta");
        FormKit.Row(t, "Carpeta destino:", _dest, FormKit.Browse(_dest, "Elegí a dónde mover los archivos"));
        FormKit.Hint(t, @"Típicamente una carpeta de Google Drive. Ej.: G:\Mi unidad\Epicrisis");
        FormKit.Row(t, "", _active);
        FormKit.OkCancel(this, t, Accept);

        _name.Text = rule.Nombre;
        _origin.Text = rule.CarpetaOrigen;
        _extensions.Text = string.Join(", ", rule.Extensiones);
        _keywords.Text = string.Join(", ", rule.PalabrasClave);
        _dest.Text = rule.CarpetaDestino;
        _active.Checked = rule.Activa;
    }

    public Rule Result { get; }

    private bool Accept()
    {
        var extensions = TextUtil.SplitList(_extensions.Text).Select(TextUtil.NormalizeExtension).Distinct().ToList();
        var keywords = TextUtil.SplitList(_keywords.Text);
        var origin = _origin.Text.Trim();
        var dest = _dest.Text.Trim();

        if (origin.Length == 0) { FormKit.Warn(this, "Indicá la carpeta origen."); return false; }
        if (extensions.Count == 0) { FormKit.Warn(this, "Indicá al menos una extensión (por ejemplo .docx)."); return false; }
        if (dest.Length == 0) { FormKit.Warn(this, "Indicá la carpeta destino."); return false; }
        if (string.Equals(TextUtil.TryNormalizeFolder(origin), TextUtil.TryNormalizeFolder(dest), StringComparison.OrdinalIgnoreCase))
        { FormKit.Warn(this, "La carpeta destino no puede ser la misma que la de origen."); return false; }
        if (!Directory.Exists(origin) && !FormKit.Confirm(this, "La carpeta origen no existe en este momento. ¿Guardar igual?")) return false;
        if (keywords.Count == 0 && !FormKit.Confirm(this,
                $"Sin palabras clave, esta regla mueve TODOS los archivos {string.Join(", ", extensions)} de la carpeta origen.\n\n¿Seguro?"))
            return false;

        Result.Nombre = _name.Text.Trim();
        Result.CarpetaOrigen = origin;
        Result.Extensiones = extensions;
        Result.PalabrasClave = keywords;
        Result.CarpetaDestino = dest;
        Result.Activa = _active.Checked;
        return true;
    }
}
