namespace Orders.Api.Application.Orders;

/// <summary>
/// Valida los requisitos de entrada definidos por Q10.
/// No consulta una base de datos ni contiene lógica de reserva de inventario:
/// el catálogo válido se recibe desde la capa que invoca la validación.
/// </summary>
public static class OrderValidator
{
    /// <summary>
    /// Devuelve errores por campo para generar ValidationProblemDetails (HTTP 400).
    /// Un SKU que sí pertenece al catálogo pero no tiene stock es un pedido válido;
    /// su rechazo eventual lo determina Inventory Worker, no esta clase.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(
        CreateOrderRequest? request,
        IReadOnlyCollection<string> catalogSkus)
    {
        ArgumentNullException.ThrowIfNull(catalogSkus);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null)
        {
            errors["pedido"] = ["El cuerpo del pedido es obligatorio."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.ClienteNombre))
            errors["clienteNombre"] = ["Ingresa el nombre del cliente."];
        else if (request.ClienteNombre.Trim().Length > 160)
            errors["clienteNombre"] = ["El nombre no puede superar 160 caracteres."];

        if (string.IsNullOrWhiteSpace(request.Sku) ||
            !catalogSkus.Any(sku => string.Equals(
                sku,
                request.Sku.Trim(),
                StringComparison.OrdinalIgnoreCase)))
        {
            errors["sku"] = ["Selecciona un SKU existente en el catálogo."];
        }

        if (!string.IsNullOrWhiteSpace(request.Sku) && request.Sku.Trim().Length > 64)
            errors["sku"] = ["El SKU no puede superar 64 caracteres."];

        if (request.Cantidad is < 1 or > 100)
            errors["cantidad"] = ["La cantidad debe ser un número entero entre 1 y 100."];

        return errors;
    }
}
