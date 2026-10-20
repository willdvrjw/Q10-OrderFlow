using OrderFlow.Messaging;
using Orders.Api.Application.Orders;

namespace Orders.Api.Infrastructure.Messaging;

/// <summary>
/// Escucha stock.reserved y stock.rejected en la misma cola durable.
/// El ACK se realiza dentro del bucle compartido solo tras finalizar
/// StockResultHandler y su transacción de base de datos.
/// </summary>
public sealed class StockResultConsumer(
    IServiceScopeFactory scopes,
    RabbitMqSettings settings,
    ILogger<StockResultConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RabbitMqConsumerLoop.RunAsync(
            settings, "orders-result-consumer", RabbitMqTopology.ResultsQueue,
            async (routingKey, payload, ct) =>
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<StockResultHandler>();
                await handler.HandleAsync(routingKey, payload, ct);
            },
            logger, stoppingToken);
}
