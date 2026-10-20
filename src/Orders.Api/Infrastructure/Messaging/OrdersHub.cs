using Microsoft.AspNetCore.SignalR;

namespace Orders.Api.Infrastructure.Messaging;

/// <summary>
/// Canal de notificación de cambios en pedidos.
/// Los clientes consultan GET /orders para obtener datos definitivos;
/// SignalR solo indica cuándo refrescar, con polling como respaldo.
/// </summary>
public sealed class OrdersHub : Hub;
