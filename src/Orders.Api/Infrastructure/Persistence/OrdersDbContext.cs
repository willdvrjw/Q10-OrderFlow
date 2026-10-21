using Microsoft.EntityFrameworkCore;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure.Persistence;

/// <summary>
/// Persistencia exclusivamente del servicio Orders.
/// No accede a tablas ni a entidades que pertenezcan a Inventory Worker.
/// </summary>
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CatalogProduct> CatalogProducts => Set<CatalogProduct>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClienteNombre).IsRequired().HasMaxLength(160);
            entity.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Cantidad).IsRequired();
            entity.Property(x => x.Estado).HasConversion<string>().IsRequired().HasMaxLength(16);
            entity.Property(x => x.MotivoRechazo).HasMaxLength(500);
            entity.HasIndex(x => x.CreadoEn);
        });

        modelBuilder.Entity<CatalogProduct>(entity =>
        {
            entity.ToTable("catalog_products");
            entity.HasKey(x => x.Sku);
            entity.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
            entity.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.RoutingKey).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Payload).IsRequired().HasColumnType("text");
            entity.HasIndex(x => x.PublishedAt);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.EventId);
            entity.HasIndex(x => x.OrderId);
        });
    }
}
