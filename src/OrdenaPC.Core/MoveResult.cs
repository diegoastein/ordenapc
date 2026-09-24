namespace OrdenaPC.Core;

public enum MoveStatus
{
    Movido,
    Simulado,
    /// <summary>El destino no está disponible; el archivo queda en su lugar y se reintenta.</summary>
    Pendiente,
    /// <summary>El archivo está abierto en otro programa; se reintenta en el próximo ciclo.</summary>
    EnUso,
    Error,
    Deshecho,
}

public sealed record MoveResult(
    DateTime Fecha,
    string Regla,
    string Origen,
    string Destino,
    MoveStatus Estado,
    string Detalle = "");
