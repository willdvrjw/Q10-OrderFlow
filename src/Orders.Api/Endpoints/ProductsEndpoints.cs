using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Api.Application.Orders;
using Orders.Api.Infrastructure.Persistence;

namespace Orders.Api.Endpoints;

public sealed record NewCatalogProductRequest(string? Sku, string? Nombre, int? Disponible);
public sealed record CatalogProductView(string Sku, string Nombre);

/// <summary>
/// Orders posee el catálogo válido para crear pedidos. Inventory es el único
/// propietario de las existencias. El alta se comunica por HTTP interno;
/// los pedidos conservan su procesamiento original por RabbitMQ.
/// </summary>
public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/products", async (
            ProductCatalog seed,
            OrdersDbContext db,
            CancellationToken ct) =>
        {
            var dynamic = await db.CatalogProducts.AsNoTracking()
                .OrderBy(product => product.Sku)
                .Select(product => new CatalogProductView(product.Sku, product.Nombre))
                .ToListAsync(ct);

            var products = seed.Products
                .Select(product => new CatalogProductView(product.Sku, product.Nombre))
                .Concat(dynamic)
                .OrderBy(product => product.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(product => product.Sku, StringComparer.Ordinal)
                .ToArray();
            return Results.Ok(products);
        });

        endpoints.MapGet("/products/{sku}", async (
            string sku,
            ProductCatalog seed,
            OrdersDbContext db,
            CancellationToken ct) =>
        {
            var normalized = sku.Trim().ToUpperInvariant();
            var seeded = seed.Products.FirstOrDefault(product =>
                string.Equals(product.Sku, normalized, StringComparison.OrdinalIgnoreCase));
            if (seeded is not null)
                return Results.Ok(new CatalogProductView(seeded.Sku, seeded.Nombre));

            var dynamic = await db.CatalogProducts.AsNoTracking()
                .Where(product => product.Sku == normalized)
                .Select(product => new CatalogProductView(product.Sku, product.Nombre))
                .SingleOrDefaultAsync(ct);
            return dynamic is null ? Results.NotFound() : Results.Ok(dynamic);
        });

        endpoints.MapPost("/products", CreateAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        NewCatalogProductRequest? request,
        ProductCatalog seed,
        OrdersDbContext db,
        InventoryProductProvisioner inventory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request is null)
        {
            errors["producto"] = ["Ingresa los datos del producto nuevo."];
            return Results.ValidationProblem(errors);
        }

        var sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
        var nombre = request.Nombre?.Trim() ?? string.Empty;

        if (sku.Length is < 1 or > 64 ||
            !System.Text.RegularExpressions.Regex.IsMatch(sku, "^[A-Z0-9][A-Z0-9._-]*$"))
            errors["sku"] = ["SKU de 1 a 64 caracteres: letras, números, puntos, guiones o guiones bajos."];
        if (nombre.Length is < 1 or > 160)
            errors["nombre"] = ["Nombre de producto obligatorio, máximo 160 caracteres."];
        if (request.Disponible is null or < 0 or > 1_000_000)
            errors["disponible"] = ["Las existencias iniciales deben estar entre 0 y 1.000.000."];

        if (errors.Count != 0)
            return Results.ValidationProblem(errors);

        if (seed.Contains(sku) ||
            await db.CatalogProducts.AsNoTracking().AnyAsync(p => p.Sku == sku, ct))
            return Results.Conflict(new { mensaje = "El SKU ya está registrado. Utiliza uno distinto." });

        try
        {
            // Preparar primero Inventory. Si falla, Orders NO publica el SKU
            // para pedidos que acabarían inevitablemente rechazados.
            // Si Orders falla luego, repetir exactamente esta solicitud
            // recupera el alta idempotente de Inventory.
            var provisioned = await inventory.EnsureInventoryAsync(
                sku, nombre, request.Disponible!.Value, ct);
            if (!provisioned)
                return Results.Conflict(new {
                    mensaje = "Ese SKU ya existe en Inventory o se intentó registrar con otros datos."
                });
        }
        catch (HttpRequestException ex)
        {
            loggerFactory.CreateLogger("ProductRegistration").LogWarning(
                ex, "Inventory no pudo aprovisionar el SKU {Sku}.", sku);
            return Results.Json(
                new { mensaje = "No se pudo conectar con Inventory para registrar el producto. Inténtalo nuevamente." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(
                new { mensaje = "Inventory tardó demasiado en responder. Reintenta el mismo SKU y los mismos datos." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        db.CatalogProducts.Add(CatalogProduct.Create(sku, nombre));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Conflict(new { mensaje = "El SKU fue registrado por otra solicitud. No se duplicó el producto." });
        }

        return Results.Created(
            "/products/" + Uri.EscapeDataString(sku), new CatalogProductView(sku, nombre));
    }
}
