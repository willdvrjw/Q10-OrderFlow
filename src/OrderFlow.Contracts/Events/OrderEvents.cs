namespace OrderFlow.Contracts.Events;

/// <summary>
/// Claves de enrutamiento estables para conectar productores y consumidores.
/// Los contratos públicos no dependen de las entidades ni bases de cada servicio.
/// </summary>
public static class OrderEventNames
{
    public const string OrderCreated = "order.created";
    public const string StockReserved = "stock.reserved";
    public const string StockRejected = "stock.rejected";
}

/// <summary>
/// Se publica tras guardar un pedido Pending y su registro de outbox
/// en la misma transacción. El consumidor identificará el efecto por OrderId.
/// </summary>
public sealed record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTimeOffset OcurridoEn,
    int SchemaVersion = 1);

/// <summary>
/// Inventory confirma que la reserva de stock quedó persistida.
/// EventId identifica la entrega y OrderId identifica el pedido a confirmar.
/// </summary>
public sealed record StockReservedEvent(
    Guid EventId,
    Guid OrderId,
    DateTimeOffset OcurridoEn,
    int SchemaVersion = 1);

/// <summary>
/// Inventory informa que no pudo reservar el stock de un pedido válido.
/// El motivo es diagnóstico y podrá mostrarse de forma clara en el frontend.
/// </summary>
public sealed record StockRejectedEvent(
    Guid EventId,
    Guid OrderId,
    string Motivo,
    DateTimeOffset OcurridoEn,
    int SchemaVersion = 1);
