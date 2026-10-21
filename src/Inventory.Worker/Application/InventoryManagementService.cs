using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Application;

/// <summary>
/// Consulta y ajusta el stock desde su único servicio propietario.
/// La condición de versión evita reemplazar existencias que cambiaron
/// por una reserva o por otro administrador entre GET y POST.
/// </summary>
public sealed class InventoryManagementService(
    InventoryDbContext db,
    ILogger<InventoryManagementService> logger,
    IInventoryChangeNotifier changes)
{
    public Task<List<StockView>> ListAsync(CancellationToken ct) =>
        db.Stock.AsNoTracking()
            .OrderBy(x => x.Sku)
            .Select(x => new StockView(x.Sku, x.Disponible))
            .ToListAsync(ct);

    public async Task<AdjustmentResult> ApplyAsync(
        string sku, string operation, int amount, int expected, CancellationToken ct)
    {
        if (operation is not ("agregar" or "establecer") ||
            amount is < 0 or > 1_000_000 ||
            (operation == "agregar" && amount == 0) ||
            expected < 0 || string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("Los datos de ajuste de existencias son inválidos.");

        // Ambos UPDATE son atómicos. En particular, no hacemos SELECT-then-write:
        // una reserva concurrente hace fallar la condición en vez de perder datos.
        int affected;
        if (operation == "agregar")
        {
            var highestSafeStock = int.MaxValue - amount;
            affected = await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE stock_items
                   SET disponible = disponible + {amount}
                   WHERE sku = {sku} AND disponible = {expected}
                     AND disponible <= {highestSafeStock}", ct);
        }
        else
        {
            affected = await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE stock_items
                   SET disponible = {amount}
                   WHERE sku = {sku} AND disponible = {expected}", ct);
        }

        var latest = await db.Stock.AsNoTracking()
            .Where(x => x.Sku == sku)
            .Select(x => (int?)x.Disponible)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
            return new AdjustmentResult(AdjustmentOutcome.NotFound, null);

        if (affected != 1)
            return new AdjustmentResult(AdjustmentOutcome.Conflict, latest);

        logger.LogInformation(
            "Inventario {Sku}: {Operation}, cantidad {Amount}, saldo previo {Previous}.",
            sku, operation, amount, expected);

        // El ajuste ya fue confirmado por PostgreSQL; una notificación fallida
        // no revierte el saldo (el navegador dispone de polling de respaldo).
        await changes.NotifyChangedAsync(sku, ct);

        return new AdjustmentResult(AdjustmentOutcome.Applied, latest);
    }
}

public sealed record StockView(string Sku, int Disponible);
public enum AdjustmentOutcome { Applied, NotFound, Conflict }
public sealed record AdjustmentResult(AdjustmentOutcome Outcome, int? Disponible);
