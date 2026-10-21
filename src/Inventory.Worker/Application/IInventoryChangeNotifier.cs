namespace Inventory.Worker.Application;

/// <summary>
/// Notifica a la interfaz cuando las existencias cambian después del COMMIT.
/// Es un efecto accesorio: las reservas y los estados siguen siendo transaccionales.
/// </summary>
public interface IInventoryChangeNotifier
{
    Task NotifyChangedAsync(string sku, CancellationToken ct);
}
