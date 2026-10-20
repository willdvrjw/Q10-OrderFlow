using Inventory.Worker.Application;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Tests;

/// <summary>
/// Dos pruebas de integración con PostgreSQL 16 real.
/// Se ejecutan aisladas en bases temporales y comprueban el efecto persistido,
/// no únicamente el valor devuelto por un método.
/// </summary>
public sealed class InventoryIntegrationTests
{
    [Fact]
    public async Task RepeatedOrderCreatedNeverDiscountsStockTwice()
    {
        await using var testDatabase = await TemporaryInventoryDatabase.CreateAsync();
        await using var db = testDatabase.OpenContext();

        const string sku = "TEST-IDEMP";
        db.Stock.Add(StockItem.Seed(sku, 10));
        await db.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var created = new OrderCreatedEvent(
            Guid.NewGuid(), orderId, sku, 4, DateTimeOffset.UtcNow);

        var handler = new OrderCreatedHandler(db);
        var first = await handler.HandleAsync(created, CancellationToken.None);

        // No solo reentregar el mismo EventId: también probar un EventId distinto
        // para el mismo OrderId, pues la unicidad del efecto es por pedido.
        var duplicate = created with { EventId = Guid.NewGuid() };
        var second = await handler.HandleAsync(duplicate, CancellationToken.None);

        Assert.Equal(OrderProcessingOutcome.Reserved, first);
        Assert.Equal(OrderProcessingOutcome.AlreadyProcessed, second);
        Assert.Equal(6, await db.Stock.AsNoTracking()
            .Where(item => item.Sku == sku)
            .Select(item => item.Disponible)
            .SingleAsync());
        Assert.Equal(1, await db.ProcessedOrders.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ConcurrentReservationsNeverMakeStockNegative()
    {
        await using var testDatabase = await TemporaryInventoryDatabase.CreateAsync();
        await using (var setup = testDatabase.OpenContext())
        {
            setup.Stock.Add(StockItem.Seed("TEST-RACE", 5));
            await setup.SaveChangesAsync();
        }

        // Contextos y transacciones separados, como dos consumidores reales.
        // Cada pedido pide 4 unidades; con 5 disponibles solo uno puede ganar.
        async Task<OrderProcessingOutcome> ProcessAsync(Guid orderId)
        {
            await using var db = testDatabase.OpenContext();
            var handler = new OrderCreatedHandler(db);
            return await handler.HandleAsync(
                new OrderCreatedEvent(
                    Guid.NewGuid(), orderId, "TEST-RACE", 4, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }

        var results = await Task.WhenAll(
            ProcessAsync(Guid.NewGuid()), ProcessAsync(Guid.NewGuid()));

        Assert.Equal(1, results.Count(result => result == OrderProcessingOutcome.Reserved));
        Assert.Equal(1, results.Count(result => result == OrderProcessingOutcome.Rejected));

        await using var verify = testDatabase.OpenContext();
        var stock = await verify.Stock.AsNoTracking()
            .Where(item => item.Sku == "TEST-RACE")
            .Select(item => item.Disponible)
            .SingleAsync();

        Assert.Equal(1, stock);
        Assert.True(stock >= 0);
        Assert.Equal(2, await verify.ProcessedOrders.CountAsync());
        Assert.Equal(2, await verify.OutboxMessages.CountAsync());
    }
}
