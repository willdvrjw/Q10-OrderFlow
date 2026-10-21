using Microsoft.AspNetCore.SignalR;

namespace Inventory.Worker.Infrastructure.Messaging;

/// <summary>
/// Canal de invalidación para el inventario: el navegador consulta GET /inventory
/// para recuperar el dato autoritativo después de cada notificación.
/// </summary>
public sealed class InventoryUpdatesHub : Hub;
