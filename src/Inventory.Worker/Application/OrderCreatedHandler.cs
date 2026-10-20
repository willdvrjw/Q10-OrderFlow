using System.Text.Json;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;

namespace Inventory.Worker.Application;

/// <summary>Resultado interno de procesamiento, independiente del transporte RabbitMQ.</summary>
public enum OrderProcessingOutcome
{
    Reserved,
    Rejected,
    AlreadyProcessed
}

/// <summary>
/// Reserva inventario de manera transaccional, segura ante concurrencia y reentregas.
/// El registro de OrderId es único: dos mensajes simultáneos del mismo pedido
/// no pueden consumir dos veces la misma existencia.
/// </summary>
public sealed class OrderCreatedHandler(InventoryDbContext db)
{
    /// <summary>
    /// La inserción de processed_orders actúa como barrera transaccional por OrderId.
    /// PostgreSQL espera la otra transacción si compite por la misma clave; solo
    /// una inserción devuelve 1 fila. El stock se actualiza con una condición atómica.
    /// Resultado y evento outbox comparten exactamente el mismo COMMIT.
    /// </summary>
    public async Task<OrderProcessingOutcome> HandleAsync(
        OrderCreatedEvent message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.OrderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.Sku) || message.Cantidad is < 1 or > 100 ||
            message.SchemaVersion != 1)
            throw new InvalidDataException("Evento OrderCreated inválido o versión no admitida.");

        var sku = message.Sku.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // La unicidad no depende de un SELECT previo vulnerable a carreras.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO processed_orders
               (order_id, event_id, sku, cantidad, resultado, ocurrido_en)
               VALUES ({message.OrderId}, {message.EventId}, {sku},
                       {message.Cantidad}, {"Processing"}, {now})
               ON CONFLICT (order_id) DO NOTHING", ct);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(ct);
            return OrderProcessingOutcome.AlreadyProcessed;
        }

        // El stock nunca puede quedar negativo, ni con solicitudes concurrentes.
        var discounted = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE stock_items SET disponible = disponible - {message.Cantidad}
               WHERE sku = {sku} AND disponible >= {message.Cantidad}", ct);

        var reserved = discounted == 1;
        var eventId = Guid.NewGuid();
        var outgoing = reserved
            ? JsonSerializer.Serialize(
                new StockReservedEvent(eventId, message.OrderId, now),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            : JsonSerializer.Serialize(
                new StockRejectedEvent(eventId, message.OrderId, "Inventario insuficiente.", now),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var eventType = reserved ? nameof(StockReservedEvent) : nameof(StockRejectedEvent);
        var routingKey = reserved ? OrderEventNames.StockReserved : OrderEventNames.StockRejected;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE processed_orders SET resultado = {(reserved ? "Reserved" : "Rejected")}
               WHERE order_id = {message.OrderId}", ct);

        db.OutboxMessages.Add(
            InventoryOutboxMessage.Create(eventId, eventType, routingKey, outgoing, now));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return reserved ? OrderProcessingOutcome.Reserved : OrderProcessingOutcome.Rejected;
    }
}
