using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Infrastructure.Persistence;

/// <summary>
/// Inicializa el esquema y los productos sin sobrescribir stock de ejecuciones previas.
/// El contenedor PostgreSQL crea la base de datos lógica; este módulo crea sus tablas.
/// </summary>
public static class InventoryDatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        const int maxAttempts = 15;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                await db.Database.EnsureCreatedAsync(ct);

                // EnsureCreated no agrega tablas a volúmenes ya inicializados.
                // DDL compatible tanto con esquemas anteriores como nuevos:
                // no elimina tablas ni reinicializa existencias.
                await db.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS inventory_product_registrations (
                        sku varchar(64) PRIMARY KEY,
                        nombre varchar(160) NOT NULL,
                        stock_inicial integer NOT NULL
                            CONSTRAINT ck_product_initial_stock
                            CHECK (stock_inicial BETWEEN 0 AND 1000000)
                      )", ct);

                var path = Path.Combine(AppContext.BaseDirectory, "seed", "products.json");
                var products = JsonSerializer.Deserialize<List<SeedProduct>>(
                    await File.ReadAllTextAsync(path, ct),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidDataException("El catálogo de Inventory está vacío.");

                if (products.Count < 3 || products.Any(p =>
                        string.IsNullOrWhiteSpace(p.Sku) || p.Disponible < 0) ||
                    products.Select(p => p.Sku.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != products.Count)
                    throw new InvalidDataException("El catálogo inicial de inventario es inválido.");

                // ON CONFLICT DO NOTHING preserva stock previamente consumido.
                // El seed solamente agrega productos nuevos al primer arranque.
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                foreach (var product in products)
                {
                    var sku = product.Sku.Trim().ToUpperInvariant();
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $@"INSERT INTO stock_items (sku, disponible)
                           VALUES ({sku}, {product.Disponible})
                           ON CONFLICT (sku) DO NOTHING", ct);
                }
                await tx.CommitAsync(ct);
                logger.LogInformation("Inventario inicializado: {Count} SKU.", products.Count);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Inventory DB no está lista: intento {Attempt}/{MaxAttempts}.",
                    attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private sealed record SeedProduct(string Sku, string Nombre, int Disponible);
}
