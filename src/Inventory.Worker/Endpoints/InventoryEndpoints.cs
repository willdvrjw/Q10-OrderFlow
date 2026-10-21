using Inventory.Worker.Application;

namespace Inventory.Worker.Endpoints;

/// <summary>
/// API de existencias de la consola: únicamente Inventory modifica stock_items.
/// Las reservas de pedidos siguen resolviéndose por eventos asincrónicos.
/// </summary>
public sealed record StockAdjustmentRequest(
    string? Sku, string? Operacion, int Cantidad, int? ExistenciaEsperada);

public sealed record NewInventoryProductRequest(string? Sku, string? Nombre, int? Disponible);

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/inventory", async (
            InventoryManagementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        endpoints.MapPost("/inventory/products", async (
            NewInventoryProductRequest? request,
            InventoryProductRegistrationService service,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (request is null)
            {
                errors["producto"] = ["Los datos del producto son obligatorios."];
                return Results.ValidationProblem(errors);
            }

            var sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
            var nombre = request.Nombre?.Trim() ?? string.Empty;
            if (sku.Length is < 1 or > 64 ||
                !System.Text.RegularExpressions.Regex.IsMatch(sku, "^[A-Z0-9][A-Z0-9._-]*$"))
                errors["sku"] = ["El SKU debe contener de 1 a 64 caracteres: letras, números, guiones, puntos o guiones bajos."];
            if (nombre.Length is < 1 or > 160)
                errors["nombre"] = ["Escribe un nombre de producto de 1 a 160 caracteres."];
            if (request.Disponible is null or < 0 or > 1_000_000)
                errors["disponible"] = ["La cantidad inicial debe ser un entero entre 0 y 1.000.000."];
            if (errors.Count != 0)
                return Results.ValidationProblem(errors);

            var result = await service.RegisterAsync(sku, nombre, request.Disponible!.Value, ct);
            return result switch
            {
                ProductRegistrationOutcome.Conflict => Results.Conflict(new {
                    mensaje = "El SKU ya existe con otros datos o tiene existencias registradas."
                }),
                ProductRegistrationOutcome.AlreadyRegistered =>
                    Results.Ok(new { sku, nombre, disponible = request.Disponible }),
                _ => Results.Created("/inventory", new { sku, nombre, disponible = request.Disponible })
            };
        });

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
