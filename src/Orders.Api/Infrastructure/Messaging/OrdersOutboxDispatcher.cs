using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;
using Orders.Api.Infrastructure.Persistence;

namespace Orders.Api.Infrastructure.Messaging;

/// <summary>
/// Despacha eventos persistidos en Orders, sin bloquear POST /orders.
/// Si RabbitMQ está caído, los pedidos continúan Pending y el evento sigue
/// guardado hasta que un envío confirmado permita marcar PublishedAt.
/// </summary>
public sealed class OrdersOutboxDispatcher(
    IServiceScopeFactory scopes,
    RabbitMqSettings settings,
    ILogger<OrdersOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await RabbitMqTransport.ConnectAsync(
                    settings, "orders-outbox", stoppingToken);
                await using var channel = await RabbitMqTransport.OpenPublisherChannelAsync(
                    connection, stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);

                while (!stoppingToken.IsCancellationRequested && channel.IsOpen && connection.IsOpen)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

                    var pending = await db.OutboxMessages
                        .Where(x => x.PublishedAt == null)
                        .OrderBy(x => x.CreatedAt)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    foreach (var message in pending)
                    {
                        // El broker confirma antes de cambiar el registro outbox.
                        // Si ocurre una caída después de confirmar, será reenviado;
                        // la unicidad por OrderId del consumidor evita doble reserva.
                        await RabbitMqTransport.PublishConfirmedAsync(
                            channel, message.Id, message.RoutingKey,
                            message.Payload, stoppingToken);

                        message.MarkPublished(DateTimeOffset.UtcNow);
                        await db.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Publicado evento {EventId} ({RoutingKey}).",
                            message.Id, message.RoutingKey);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "RabbitMQ no disponible para la outbox de Orders; mensajes conservados.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
