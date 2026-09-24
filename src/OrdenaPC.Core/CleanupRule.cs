using System.Runtime.Serialization;

namespace OrdenaPC.Core;

/// <summary>
/// Limpieza de una carpeta: los archivos que no coinciden con ninguna regla y llevan más de
/// Dias sin cambios se mueven a CarpetaDestino (opcionalmente en una subcarpeta por mes).
/// </summary>
public sealed class CleanupRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CarpetaOrigen { get; set; } = "";
    public int Dias { get; set; } = 30;
    public string CarpetaDestino { get; set; } = "";
    public bool SubcarpetaPorMes { get; set; } = true;
    public bool Activa { get; set; } = true;

    [IgnoreDataMember]
    public string NombreRegla
    {
        get
        {
            var folder = TextUtil.TryNormalizeFolder(CarpetaOrigen);
            var name = folder.Length > 0 ? Path.GetFileName(folder) : "";
            return "Limpieza: " + (string.IsNullOrEmpty(name) ? CarpetaOrigen : name);
        }
    }

    public CleanupRule Clone() => (CleanupRule)MemberwiseClone();
}
