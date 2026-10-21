using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Infrastructure.Persistence;

/// <summary>
/// Base de datos propia de Inventory. No comparte entidades ni transacciones
/// con Orders API: la comunicación entre servicios se realiza por RabbitMQ.
/// </summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> Stock => Set<StockItem>();
    public DbSet<ProcessedOrder> ProcessedOrders => Set<ProcessedOrder>();
    public DbSet<InventoryOutboxMessage> OutboxMessages => Set<InventoryOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.ToTable("stock_items");
            entity.HasKey(x => x.Sku);
            entity.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
            entity.Property(x => x.Disponible).HasColumnName("disponible");
            entity.ToTable(t => t.HasCheckConstraint("ck_stock_nonnegative", "disponible >= 0"));
        });

        modelBuilder.Entity<ProcessedOrder>(entity =>
        {
            entity.ToTable("processed_orders");
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Cantidad).HasColumnName("cantidad");
            entity.Property(x => x.Resultado).HasColumnName("resultado").HasMaxLength(16).IsRequired();
            entity.Property(x => x.OcurridoEn).HasColumnName("ocurrido_en");
        });

        modelBuilder.Entity<InventoryOutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RoutingKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("text").IsRequired();
            entity.HasIndex(x => x.PublishedAt);
        });
    }
}

/// <summary>Stock actual; solamente Inventory puede modificarlo.</summary>
public sealed class StockItem
{
    private StockItem() { }

    public string Sku { get; private set; } = string.Empty;
    public int Disponible { get; private set; }

    public static StockItem Seed(string sku, int disponible)
    {
        if (string.IsNullOrWhiteSpace(sku) || disponible < 0)
            throw new ArgumentException("SKU y cantidad inicial deben ser válidos.");

        return new StockItem { Sku = sku.Trim().ToUpperInvariant(), Disponible = disponible };
    }
}

/// <summary>
/// Clave única por pedido para garantizar que su efecto sobre el inventario
/// ocurra como máximo una vez, incluso si RabbitMQ reentrega el evento.
/// </summary>
public sealed class ProcessedOrder
{
    private ProcessedOrder() { }

    public Guid OrderId { get; private set; }
    public Guid EventId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Cantidad { get; private set; }
    public string Resultado { get; private set; } = string.Empty;
    public DateTimeOffset OcurridoEn { get; private set; }
}

/// <summary>Publicación eventual del resultado de inventario, persistida en el mismo COMMIT.</summary>
public sealed class InventoryOutboxMessage
{
    private InventoryOutboxMessage() { }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static InventoryOutboxMessage Create(
        Guid id, string eventType, string routingKey, string payload, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(routingKey) || string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Datos incompletos para crear un evento outbox.");

        return new InventoryOutboxMessage
        {
            Id = id,
            EventType = eventType,
            RoutingKey = routingKey,
            Payload = payload,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public void MarkPublished(DateTimeOffset at) => PublishedAt ??= at.ToUniversalTime();
}
