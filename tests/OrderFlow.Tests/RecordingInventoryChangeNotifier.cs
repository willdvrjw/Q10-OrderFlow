using System.Collections.Concurrent;
using Inventory.Worker.Application;

namespace OrderFlow.Tests;

/// <summary>
/// Permite comprobar las notificaciones producidas por reservas y ajustes
/// sin levantar SignalR; la persistencia sigue comprobándose en PostgreSQL.
/// </summary>
internal sealed class RecordingInventoryChangeNotifier : IInventoryChangeNotifier
{
    private readonly ConcurrentQueue<string> _skus = new();

    public int Count => _skus.Count;

    public Task NotifyChangedAsync(string sku, CancellationToken ct)
    {
        _skus.Enqueue(sku);
        return Task.CompletedTask;
    }
}
