using RabbitMQ.Client;

namespace OrderFlow.Messaging;

/// <summary>
/// Topología declarativa compartida: cualquier servicio puede arrancar primero.
/// Las colas son durables y los mensajes defectuosos se envían a colas de diagnóstico.
/// </summary>
public static class RabbitMqTopology
{
    public const string Exchange = "orderflow.events";
    public const string DeadExchange = "orderflow.dead";
    public const string CreatedQueue = "orderflow.inventory.created";
    public const string ResultsQueue = "orderflow.orders.results";
    public const string CreatedDeadQueue = "orderflow.inventory.dead";
    public const string ResultsDeadQueue = "orderflow.orders.dead";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            exchange: Exchange, type: ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(
            exchange: DeadExchange, type: ExchangeType.Direct, durable: true,
            autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(CreatedDeadQueue, durable: true,
            exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(ResultsDeadQueue, durable: true,
            exclusive: false, autoDelete: false, cancellationToken: ct);

        await channel.QueueBindAsync(CreatedDeadQueue, DeadExchange,
            "created.dead", cancellationToken: ct);
        await channel.QueueBindAsync(ResultsDeadQueue, DeadExchange,
            "results.dead", cancellationToken: ct);

        await channel.QueueDeclareAsync(CreatedQueue, durable: true,
            exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadExchange,
                ["x-dead-letter-routing-key"] = "created.dead"
            }, cancellationToken: ct);

        await channel.QueueDeclareAsync(ResultsQueue, durable: true,
            exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadExchange,
                ["x-dead-letter-routing-key"] = "results.dead"
            }, cancellationToken: ct);

        await channel.QueueBindAsync(CreatedQueue, Exchange, "order.created",
            cancellationToken: ct);
        await channel.QueueBindAsync(ResultsQueue, Exchange, "stock.reserved",
            cancellationToken: ct);
        await channel.QueueBindAsync(ResultsQueue, Exchange, "stock.rejected",
            cancellationToken: ct);
    }
}
