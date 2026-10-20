using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace OrderFlow.Messaging;

/// <summary>
/// Bucle reutilizable con conexiones renovables, consumo manual y tratamiento
/// separado de mensajes inválidos y fallos temporales de infraestructura.
/// No contiene lógica específica de Orders ni de Inventory.
/// </summary>
public static class RabbitMqConsumerLoop
{
    public static async Task RunAsync(
        RabbitMqSettings settings,
        string connectionName,
        string queue,
        Func<string, byte[], CancellationToken, Task> handle,
        ILogger logger,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var connection = await RabbitMqTransport.ConnectAsync(
                    settings, connectionName, ct);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: ct);

                await RabbitMqTopology.DeclareAsync(channel, ct);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1,
                    global: false, cancellationToken: ct);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    // RabbitMQ reutiliza el búfer: copiar antes de cualquier await.
                    var body = delivery.Body.ToArray();
                    try
                    {
                        await handle(delivery.RoutingKey, body, ct);
                        // ACK solo después de confirmar la transacción del handler.
                        await channel.BasicAckAsync(delivery.DeliveryTag,
                            multiple: false, cancellationToken: ct);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidDataException)
                    {
                        logger.LogError(ex,
                            "Mensaje inválido enviado a DLQ desde {Queue}.", queue);
                        await channel.BasicRejectAsync(delivery.DeliveryTag,
                            requeue: false, cancellationToken: ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // La conexión se cerrará al detener el proceso;
                        // el mensaje sin ACK volverá a estar disponible.
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Fallo transitorio al consumir {Queue}; se reintentará.", queue);
                        // Retraso deliberado para impedir bucles de reentrega intensa.
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        await channel.BasicNackAsync(delivery.DeliveryTag,
                            multiple: false, requeue: true, cancellationToken: ct);
                    }
                };

                await channel.BasicConsumeAsync(queue, autoAck: false,
                    consumer: consumer, cancellationToken: ct);
                logger.LogInformation("Consumiendo eventos desde {Queue}.", queue);

                while (!ct.IsCancellationRequested && connection.IsOpen && channel.IsOpen)
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Conexión RabbitMQ de {Queue} interrumpida; reconectando.", queue);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
