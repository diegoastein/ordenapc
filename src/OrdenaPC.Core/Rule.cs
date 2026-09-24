using System.Text.Json.Serialization;

namespace OrdenaPC.Core;

public sealed class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string CarpetaOrigen { get; set; } = "";
    public List<string> Extensiones { get; set; } = new();
    public List<string> PalabrasClave { get; set; } = new();
    public string CarpetaDestino { get; set; } = "";
    public bool Activa { get; set; } = true;

    [JsonIgnore]
    public string NombreVisible => string.IsNullOrWhiteSpace(Nombre)
        ? $"{string.Join(",", PalabrasClave)} {string.Join(",", Extensiones)}".Trim()
        : Nombre;

    public Rule Clone() => new()
    {
        Id = Id,
        Nombre = Nombre,
        CarpetaOrigen = CarpetaOrigen,
        Extensiones = new(Extensiones),
        PalabrasClave = new(PalabrasClave),
        CarpetaDestino = CarpetaDestino,
        Activa = Activa,
    };
}
