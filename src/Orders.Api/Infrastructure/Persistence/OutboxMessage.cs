namespace Orders.Api.Infrastructure.Persistence;

/// <summary>
/// Evento almacenado en la misma base que el pedido, antes de enviarlo a RabbitMQ.
/// PublishedAt permanece nulo hasta que el broker confirme recepción.
/// Una caída del broker no hace perder este mensaje.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        string eventType,
        string routingKey,
        string payload,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El identificador del evento es obligatorio.", nameof(id));
        if (string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(routingKey) ||
            string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Un evento necesita tipo, ruta y contenido.");

        return new OutboxMessage
        {
            Id = id,
            EventType = eventType,
            RoutingKey = routingKey,
            Payload = payload,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public void MarkPublished(DateTimeOffset at) =>
        PublishedAt ??= at.ToUniversalTime();
}
