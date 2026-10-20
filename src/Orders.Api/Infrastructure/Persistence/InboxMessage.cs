namespace Orders.Api.Infrastructure.Persistence;

/// <summary>
/// Registro de resultados de inventario ya aplicados. EventId es clave primaria:
/// una reentrega no debe modificar el pedido dos veces.
/// La inserción se hará en la misma transacción que la transición de estado.
/// </summary>
public sealed class InboxMessage
{
    private InboxMessage() { }

    public Guid EventId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Processed(Guid eventId, Guid orderId, DateTimeOffset at)
    {
        if (eventId == Guid.Empty || orderId == Guid.Empty)
            throw new ArgumentException("Los IDs de evento y pedido deben ser válidos.");

        return new InboxMessage
        {
            EventId = eventId,
            OrderId = orderId,
            ProcessedAt = at.ToUniversalTime()
        };
    }
}
