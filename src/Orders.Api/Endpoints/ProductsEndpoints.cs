using Orders.Api.Application.Orders;

namespace Orders.Api.Endpoints;

/// <summary>
/// Catálogo de solo lectura para el formulario. No expone disponibilidad
/// porque el stock actual pertenece exclusivamente a Inventory Worker.
/// </summary>
public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/products", (ProductCatalog catalog) =>
            Results.Ok(catalog.Products
                .Select(product => new { product.Sku, product.Nombre })
                .ToArray()));
        return endpoints;
    }
}
