namespace Orders.Api.Domain;

/// <summary>
/// Estados permitidos de un pedido. Pending significa que la reserva de stock aún no ha concluido.
/// Confirmed y Rejected son terminales: los eventos duplicados no pueden revertirlos.
/// </summary>
public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected
}
