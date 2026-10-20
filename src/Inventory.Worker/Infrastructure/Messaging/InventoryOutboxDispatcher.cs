using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;

namespace Inventory.Worker.Infrastructure.Messaging;

/// <summary>
/// Publica los resultados de reserva desde la outbox propia de Inventory.
/// Un fallo del broker no pierde el resultado de la transacción de stock.
/// </summary>
public sealed class InventoryOutboxDispatcher(
    IServiceScopeFactory scopes,
    RabbitMqSettings settings,
    ILogger<InventoryOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await RabbitMqTransport.ConnectAsync(
                    settings, "inventory-outbox", stoppingToken);
                await using var channel = await RabbitMqTransport.OpenPublisherChannelAsync(
                    connection, stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);

                while (!stoppingToken.IsCancellationRequested && connection.IsOpen && channel.IsOpen)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

                    var pending = await db.OutboxMessages
                        .Where(x => x.PublishedAt == null)
                        .OrderBy(x => x.CreatedAt)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    foreach (var message in pending)
                    {
                        await RabbitMqTransport.PublishConfirmedAsync(
                            channel, message.Id, message.RoutingKey,
                            message.Payload, stoppingToken);
                        message.MarkPublished(DateTimeOffset.UtcNow);
                        await db.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Resultado de stock {EventId} publicado.", message.Id);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Broker inaccesible para resultados de inventario; outbox conservada.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
