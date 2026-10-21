using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Application;

/// <summary>
/// Alta idempotente de un SKU y su existencia inicial en UNA transacción de Inventory.
/// El nombre y la cantidad original permiten distinguir una repetición legítima
/// de un intento posterior de reutilizar el mismo SKU con otros datos.
/// </summary>
public sealed class InventoryProductRegistrationService(
    InventoryDbContext db,
    IInventoryChangeNotifier changes,
    ILogger<InventoryProductRegistrationService> logger)
{
    public async Task<ProductRegistrationOutcome> RegisterAsync(
        string sku, string nombre, int stockInicial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(nombre) ||
            stockInicial is < 0 or > 1_000_000)
            throw new ArgumentException("Datos inválidos para registrar un producto.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // La PK bloquea altas paralelas del mismo SKU y ON CONFLICT evita
        // excepciones o duplicados si Orders reintenta una creación incompleta.
        var registered = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO inventory_product_registrations (sku, nombre, stock_inicial)
               VALUES ({sku}, {nombre}, {stockInicial})
               ON CONFLICT (sku) DO NOTHING", ct);

        if (registered == 0)
        {
            var existing = await db.ProductRegistrations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Sku == sku, ct);
            await transaction.RollbackAsync(ct);
            return existing is not null &&
                string.Equals(existing.Nombre, nombre, StringComparison.Ordinal) &&
                existing.StockInicial == stockInicial
                ? ProductRegistrationOutcome.AlreadyRegistered
                : ProductRegistrationOutcome.Conflict;
        }

        // Nunca reutilizar ni sobrescribir stock previamente existente:
        // incluye productos originales del archivo seed.
        var insertedStock = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO stock_items (sku, disponible)
               VALUES ({sku}, {stockInicial})
               ON CONFLICT (sku) DO NOTHING", ct);

        if (insertedStock != 1)
        {
            await transaction.RollbackAsync(ct);
            return ProductRegistrationOutcome.Conflict;
        }

        await transaction.CommitAsync(ct);
        logger.LogInformation("SKU {Sku} incorporado con {Quantity} unidades.", sku, stockInicial);
        await changes.NotifyChangedAsync(sku, ct);
        return ProductRegistrationOutcome.Created;
    }
}

public enum ProductRegistrationOutcome { Created, AlreadyRegistered, Conflict }
