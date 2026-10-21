using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;
using Orders.Api.Application.Orders;
using Orders.Api.Domain;
using Orders.Api.Infrastructure.Persistence;

namespace Orders.Api.Endpoints;

/// <summary>
/// Único punto de entrada HTTP para los pedidos.
/// Valida en la API aun si el frontend ya validó, y nunca confirma inventario
/// de manera síncrona durante el POST.
/// </summary>
public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/orders", CreateOrderAsync);
        endpoints.MapGet("/orders", ListOrdersAsync);
        endpoints.MapGet("/orders/{id:guid}", GetOrderAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest? request,
        ProductCatalog catalog,
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        // La validación de Q10 sigue siendo idéntica: solo acepta SKU del
        // catálogo. Ahora este incluye los registrados y persistidos por Orders.
        IReadOnlyCollection<string> validSkus = catalog.Skus;
        if (!string.IsNullOrWhiteSpace(request?.Sku) && !catalog.Contains(request.Sku))
        {
            var normalized = request.Sku.Trim().ToUpperInvariant();
            if (await db.CatalogProducts.AsNoTracking()
                .AnyAsync(product => product.Sku == normalized, cancellationToken))
                validSkus = catalog.Skus.Concat([normalized]).ToArray();
        }

        var errors = OrderValidator.Validate(request, validSkus);
        if (errors.Count != 0)
            return Results.ValidationProblem(errors.ToDictionary(k => k.Key, v => v.Value));

        // La validación anterior garantiza estos campos en el request.
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(
            Guid.NewGuid(), request!.ClienteNombre!, request.Sku!, request.Cantidad, now);

        var integrationEvent = new OrderCreatedEvent(
            Guid.NewGuid(), order.Id, order.Sku, order.Cantidad, now);
        var outboxMessage = OutboxMessage.Create(
            integrationEvent.EventId,
            nameof(OrderCreatedEvent),
            OrderEventNames.OrderCreated,
            JsonSerializer.Serialize(integrationEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            now);

        // PEDIDO + EVENTO en la misma transacción local:
        // al fallar SaveChanges se revierte todo; no queda un pedido sin evento.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Orders.Add(order);
        db.OutboxMessages.Add(outboxMessage);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/orders/{order.Id}", ToResponse(order));
    }

    private static async Task<IResult> ListOrdersAsync(
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreadoEn)
            .ToListAsync(cancellationToken);
        return Results.Ok(orders.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetOrderAsync(
        Guid id,
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(ToResponse(order));
    }

    /// <summary>
    /// Respuesta independiente de la entidad de base de datos.
    /// Convertir Estado explícitamente a texto preserva los valores del PDF.
    /// </summary>
    private static OrderResponse ToResponse(Order order) =>
        new(order.Id, order.ClienteNombre, order.Sku, order.Cantidad,
            order.Estado.ToString(), order.CreadoEn,
            order.ActualizadoEn, order.MotivoRechazo);

    private sealed record OrderResponse(
        Guid Id, string ClienteNombre, string Sku, int Cantidad,
        string Estado, DateTimeOffset CreadoEn,
        DateTimeOffset? ActualizadoEn, string? MotivoRechazo);
}
