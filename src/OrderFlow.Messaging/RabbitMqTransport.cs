using System.Text;
using RabbitMQ.Client;

namespace OrderFlow.Messaging;

/// <summary>
/// Operaciones compartidas de transporte sin conocimiento de pedidos ni inventario.
/// Cada despachador conserva una conexión y un canal por ciclo de vida.
/// </summary>
public static class RabbitMqTransport
{
    public static Task<IConnection> ConnectAsync(
        RabbitMqSettings settings, string connectionName, CancellationToken ct) =>
        settings.CreateFactory(connectionName).CreateConnectionAsync(ct);

    /// <summary>
    /// Activa confirmaciones de publicación y su seguimiento en el canal.
    /// Solo si BasicPublishAsync finaliza se puede marcar un evento como enviado.
    /// mandatory=true detecta rutas sin cola y evita descartar publicaciones.
    /// </summary>
    public static Task<IChannel> OpenPublisherChannelAsync(
        IConnection connection, CancellationToken ct) =>
        connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            ct);

    public static async Task PublishConfirmedAsync(
        IChannel channel, Guid eventId, string routingKey,
        string json, CancellationToken ct)
    {
        var props = new BasicProperties
        {
            MessageId = eventId.ToString("D"),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: RabbitMqTopology.Exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes(json),
            cancellationToken: ct);
    }
}
