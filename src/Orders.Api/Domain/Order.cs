namespace Orders.Api.Domain;

/// <summary>
/// Entidad de dominio del pedido.
/// Mantiene sus invariantes sin conocer HTTP, EF Core ni RabbitMQ.
/// El acceso de escritura es privado para impedir cambios de estado arbitrarios.
/// </summary>
public sealed class Order
{
    // EF Core podrá materializar entidades almacenadas cuando integremos persistencia.
    private Order() { }

    public Guid Id { get; private set; }
    public string ClienteNombre { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public int Cantidad { get; private set; }
    public OrderStatus Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? ActualizadoEn { get; private set; }
    public string? MotivoRechazo { get; private set; }

    /// <summary>
    /// Crea una entidad válida exclusivamente en Pending. La comprobación de que el SKU
    /// exista en el catálogo corresponde a la capa de aplicación, no a esta entidad.
    /// Recibir la fecha como argumento facilita pruebas deterministas.
    /// </summary>
    public static Order Create(
        Guid id,
        string clienteNombre,
        string sku,
        int cantidad,
        DateTimeOffset creadoEn)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El identificador del pedido es obligatorio.", nameof(id));
        if (string.IsNullOrWhiteSpace(clienteNombre))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(clienteNombre));
        if (clienteNombre.Trim().Length > 160)
            throw new ArgumentException("El nombre no puede superar 160 caracteres.", nameof(clienteNombre));
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU es obligatorio.", nameof(sku));
        if (sku.Trim().Length > 64)
            throw new ArgumentException("El SKU no puede superar 64 caracteres.", nameof(sku));
        if (cantidad is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe estar entre 1 y 100.");

        return new Order
        {
            Id = id,
            ClienteNombre = clienteNombre.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Cantidad = cantidad,
            Estado = OrderStatus.Pending,
            CreadoEn = creadoEn.ToUniversalTime()
        };
    }

    /// <summary>
    /// Confirma únicamente un pedido Pending. Devuelve false si ya es terminal,
    /// incluso si otro evento intenta confirmar el mismo pedido de nuevo.
    /// El consumidor registrará esos eventos sin sobrescribir el resultado.
    /// </summary>
    public bool TryConfirm(DateTimeOffset actualizadoEn)
    {
        if (Estado != OrderStatus.Pending)
            return false;

        Estado = OrderStatus.Confirmed;
        ActualizadoEn = actualizadoEn.ToUniversalTime();
        return true;
    }

    /// <summary>
    /// Rechaza únicamente un pedido Pending y conserva el motivo para el operador.
    /// No se permite transformar un pedido previamente confirmado o rechazado.
    /// </summary>
    public bool TryReject(DateTimeOffset actualizadoEn, string motivo)
    {
        if (Estado != OrderStatus.Pending)
            return false;

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivo));

        Estado = OrderStatus.Rejected;
        MotivoRechazo = motivo.Trim();
        ActualizadoEn = actualizadoEn.ToUniversalTime();
        return true;
    }
}
