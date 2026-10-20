using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;
using Orders.Api.Infrastructure.Messaging;
using Orders.Api.Infrastructure.Persistence;

namespace Orders.Api.Application.Orders;

/// <summary>
/// Aplica los resultados del inventario con consistencia eventual.
/// La bandeja Inbox y la transición del pedido se confirman juntas,
/// de modo que redeliveries no alteran un estado ya definitivo.
/// </summary>
public sealed class StockResultHandler(
    OrdersDbContext db,
    IHubContext<OrdersHub> hub,
    ILogger<StockResultHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(
        string routingKey, byte[] payload, CancellationToken ct)
    {
        Guid eventId;
        Guid orderId;
        bool reserved;
        string? reason = null;

        if (routingKey == OrderEventNames.StockReserved)
        {
            var message = JsonSerializer.Deserialize<StockReservedEvent>(payload, JsonOptions)
                ?? throw new InvalidDataException("Resultado de reserva vacío.");
            (eventId, orderId, reserved) = (message.EventId, message.OrderId, true);
            if (message.SchemaVersion != 1)
                throw new InvalidDataException("Versión de reserva no admitida.");
        }
        else if (routingKey == OrderEventNames.StockRejected)
        {
            var message = JsonSerializer.Deserialize<StockRejectedEvent>(payload, JsonOptions)
                ?? throw new InvalidDataException("Resultado de rechazo vacío.");
            (eventId, orderId, reserved) = (message.EventId, message.OrderId, false);
            reason = message.Motivo;
            if (message.SchemaVersion != 1 || string.IsNullOrWhiteSpace(reason))
                throw new InvalidDataException("Resultado de rechazo inválido.");
        }
        else
        {
            throw new InvalidDataException("Ruta de evento de inventario desconocida.");
        }

        if (eventId == Guid.Empty || orderId == Guid.Empty)
            throw new InvalidDataException("Evento de inventario sin identificadores.");

        var now = DateTimeOffset.UtcNow;
        bool changed;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            // La PK de Inbox es EventId. Ante carreras, un segundo consumidor
            // puede reintentar tras un conflicto y observar el resultado ya guardado.
            if (await db.InboxMessages.AnyAsync(x => x.EventId == eventId, ct))
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new InvalidDataException("Evento sin pedido correspondiente.");

            changed = reserved
                ? order.TryConfirm(now)
                : order.TryReject(now, reason!);

            db.InboxMessages.Add(InboxMessage.Processed(eventId, orderId, now));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (changed)
        {
            logger.LogInformation(
                "Pedido {OrderId} actualizado a {Result}.", orderId,
                reserved ? "Confirmed" : "Rejected");

            try
            {
                // La señalización no participa de la transacción:
                // su fallo no puede revertir el pedido ya confirmado.
                await hub.Clients.All.SendAsync(
                    "ordersChanged", new { orderId }, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "No se notificó el pedido {OrderId}; polling recuperará el estado.", orderId);
            }
        }
    }
}
