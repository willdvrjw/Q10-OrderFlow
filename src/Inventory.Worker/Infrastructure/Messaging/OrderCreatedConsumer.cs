using System.Text.Json;
using Inventory.Worker.Application;
using OrderFlow.Contracts.Events;
using OrderFlow.Messaging;

namespace Inventory.Worker.Infrastructure.Messaging;

/// <summary>
/// Recibe OrderCreated, invoca la reserva transaccional y reconoce el mensaje
/// solo después del COMMIT. Las reentregas son normales y seguras por OrderId.
/// </summary>
public sealed class OrderCreatedConsumer(
    IServiceScopeFactory scopes,
    RabbitMqSettings settings,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RabbitMqConsumerLoop.RunAsync(
            settings, "inventory-order-consumer", RabbitMqTopology.CreatedQueue,
            async (routingKey, payload, ct) =>
            {
                if (routingKey != OrderEventNames.OrderCreated)
                    throw new InvalidDataException("Evento no admitido por Inventory.");

                var message = JsonSerializer.Deserialize<OrderCreatedEvent>(payload, JsonOptions)
                    ?? throw new InvalidDataException("Evento OrderCreated sin contenido.");

                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<OrderCreatedHandler>();
                var outcome = await handler.HandleAsync(message, ct);

                logger.LogInformation(
                    "Pedido {OrderId} en Inventory: {Outcome}.", message.OrderId, outcome);
            },
            logger, stoppingToken);
}
