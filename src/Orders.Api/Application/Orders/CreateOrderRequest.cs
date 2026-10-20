namespace Orders.Api.Application.Orders;

/// <summary>
/// Datos de entrada del POST /orders.
/// Se permiten valores nulos en el contrato para que la validación devuelva
/// HTTP 400 con mensajes legibles, en vez de asumir que el cliente envió todo.
/// </summary>
public sealed record CreateOrderRequest(string? ClienteNombre, string? Sku, int Cantidad);
