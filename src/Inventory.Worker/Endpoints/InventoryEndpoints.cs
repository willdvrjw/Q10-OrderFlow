using Inventory.Worker.Application;

namespace Inventory.Worker.Endpoints;

/// <summary>
/// API administrativa de DEMOSTRACIÓN. Inventory mantiene la propiedad
/// de stock_items; Orders nunca lee ni modifica esta tabla.
/// Para producción se requiere autenticación, autorización y auditoría.
/// </summary>
public sealed record StockAdjustmentRequest(
    string? Sku, string? Operacion, int Cantidad, int? ExistenciaEsperada);

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/inventory", async (
            InventoryManagementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        endpoints.MapPost("/inventory/adjustments", async (
            StockAdjustmentRequest? request,
            InventoryManagementService service,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (request is null)
            {
                errors["ajuste"] = ["Los datos del ajuste son obligatorios."];
                return Results.ValidationProblem(errors);
            }

            if (string.IsNullOrWhiteSpace(request.Sku) || request.Sku.Trim().Length > 64)
                errors["sku"] = ["Selecciona un producto válido."];
            if (request.Operacion is not ("agregar" or "establecer"))
                errors["operacion"] = ["Selecciona agregar unidades o establecer existencias."];
            if (request.Cantidad > 1_000_000 || request.Cantidad < 0 ||
                (request.Operacion == "agregar" && request.Cantidad == 0))
                errors["cantidad"] = ["Agrega entre 1 y 1.000.000 unidades, o establece entre 0 y 1.000.000."];
            if (request.ExistenciaEsperada is null or < 0)
                errors["existenciaEsperada"] = ["Actualiza la información antes de guardar."];

            if (errors.Count != 0)
                return Results.ValidationProblem(errors);

            var sku = request.Sku!.Trim().ToUpperInvariant();
            var outcome = await service.ApplyAsync(
                sku, request.Operacion!, request.Cantidad,
                request.ExistenciaEsperada!.Value, ct);

            return outcome.Outcome switch
            {
                AdjustmentOutcome.NotFound => Results.NotFound(
                    new { mensaje = "El SKU no existe en el inventario." }),
                AdjustmentOutcome.Conflict => Results.Conflict(
                    new { mensaje = "Las existencias cambiaron. Actualiza y vuelve a intentar.", disponibleActual = outcome.Disponible }),
                _ => Results.Ok(new {
                    sku,
                    disponible = outcome.Disponible,
                    operacion = request.Operacion,
                    cantidad = request.Cantidad
                })
            };
        });

        return endpoints;
    }
}
