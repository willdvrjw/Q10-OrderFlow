using Inventory.Worker.Application;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Tests;

/// <summary>
/// Pruebas de nuevas altas contra PostgreSQL real, sin mocks de persistencia.
/// Complementan las cinco pruebas originales de reglas de Q10.
/// </summary>
public sealed class InventoryProductRegistrationTests
{
    [Fact]
    public async Task NewProductIsAvailableForReservationsAndRemainsIdempotent()
    {
        await using var database = await TemporaryInventoryDatabase.CreateAsync();
        var notifications = new RecordingInventoryChangeNotifier();

        await using (var db = database.OpenContext())
        {
            var registration = new InventoryProductRegistrationService(
                db, notifications,
                NullLogger<InventoryProductRegistrationService>.Instance);
            var created = await registration.RegisterAsync(
                "NEW-SKU-04", "Producto nuevo", 12, CancellationToken.None);
            Assert.Equal(ProductRegistrationOutcome.Created, created);
        }

        // Contexto distinto: producto persistido, no solo caché de memoria.
        await using (var db = database.OpenContext())
        {
            var handler = new OrderCreatedHandler(db, notifications);
            var outcome = await handler.HandleAsync(
                new OrderCreatedEvent(
                    Guid.NewGuid(), Guid.NewGuid(), "NEW-SKU-04", 4, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.Equal(OrderProcessingOutcome.Reserved, outcome);
        }

        await using (var db = database.OpenContext())
        {
            var registration = new InventoryProductRegistrationService(
                db, notifications,
                NullLogger<InventoryProductRegistrationService>.Instance);
            var retry = await registration.RegisterAsync(
                "NEW-SKU-04", "Producto nuevo", 12, CancellationToken.None);
            var incompatible = await registration.RegisterAsync(
                "NEW-SKU-04", "Producto diferente", 12, CancellationToken.None);
            Assert.Equal(ProductRegistrationOutcome.AlreadyRegistered, retry);
            Assert.Equal(ProductRegistrationOutcome.Conflict, incompatible);
            Assert.Equal(1, await db.ProductRegistrations.CountAsync());
            Assert.Equal(8, await db.Stock.AsNoTracking()
                .Where(x => x.Sku == "NEW-SKU-04")
                .Select(x => x.Disponible).SingleAsync());
            Assert.Equal(2, notifications.Count); // alta y reserva, no reintentos
        }
    }

    [Fact]
    public async Task NewProductCannotOverwriteExistingSeedStock()
    {
        await using var database = await TemporaryInventoryDatabase.CreateAsync();
        await using var db = database.OpenContext();
        db.Stock.Add(StockItem.Seed("EXISTING-01", 9));
        await db.SaveChangesAsync();

        var notifications = new RecordingInventoryChangeNotifier();
        var registration = new InventoryProductRegistrationService(
            db, notifications,
            NullLogger<InventoryProductRegistrationService>.Instance);
        var outcome = await registration.RegisterAsync(
            "EXISTING-01", "Nombre distinto", 50, CancellationToken.None);

        Assert.Equal(ProductRegistrationOutcome.Conflict, outcome);
        Assert.Equal(0, await db.ProductRegistrations.CountAsync());
        Assert.Equal(9, await db.Stock.AsNoTracking()
            .Where(x => x.Sku == "EXISTING-01")
            .Select(x => x.Disponible).SingleAsync());
        Assert.Equal(0, notifications.Count);
    }
}
