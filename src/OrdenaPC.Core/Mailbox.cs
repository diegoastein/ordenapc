using System.Runtime.Serialization;

namespace OrdenaPC.Core;

/// <summary>Ventanita en el escritorio: lo que se arrastra encima va a CarpetaDestino.</summary>
public sealed class Mailbox
{
    public const int DefaultColor = unchecked((int)0xFF2E7D32);
    public const int DefaultWidth = 220;
    public const int DefaultHeight = 150;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    /// <summary>Color ARGB.</summary>
    public int Color { get; set; } = DefaultColor;
    public int Ancho { get; set; } = DefaultWidth;
    public int Alto { get; set; } = DefaultHeight;
    /// <summary>Posición en pantalla; -1 = ubicar automáticamente.</summary>
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
    public string CarpetaDestino { get; set; } = "";
    public bool Activo { get; set; } = true;

    [IgnoreDataMember]
    public string NombreRegla => "Buzón: " + Nombre;

    public Mailbox Clone() => (Mailbox)MemberwiseClone();
}
