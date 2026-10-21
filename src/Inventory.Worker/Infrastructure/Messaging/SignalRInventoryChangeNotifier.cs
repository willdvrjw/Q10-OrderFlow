using Inventory.Worker.Application;
using Microsoft.AspNetCore.SignalR;

namespace Inventory.Worker.Infrastructure.Messaging;

/// <summary>
/// Señaliza cambios confirmados de inventario. Si el canal no está disponible,
/// el polling del navegador vuelve a sincronizar las existencias.
/// </summary>
public sealed class SignalRInventoryChangeNotifier(
    IHubContext<InventoryUpdatesHub> hub,
    ILogger<SignalRInventoryChangeNotifier> logger) : IInventoryChangeNotifier
{
    public async Task NotifyChangedAsync(string sku, CancellationToken ct)
    {
        try
        {
            await hub.Clients.All.SendAsync("inventoryChanged", new { sku }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo notificar el cambio de existencias de {Sku}; el navegador consultará nuevamente.",
                sku);
        }
    }
}
