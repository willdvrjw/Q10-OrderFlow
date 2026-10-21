using System.Net;
using System.Net.Http.Json;

namespace Orders.Api.Application.Orders;

/// <summary>
/// Aprovisiona el SKU dentro del único servicio propietario de existencias.
/// El alta remota es idempotente para soportar reintentos de una petición
/// cuando Inventory confirmó pero Orders aún no guardó su catálogo local.
/// </summary>
public sealed class InventoryProductProvisioner(HttpClient client)
{
    public async Task<bool> EnsureInventoryAsync(
        string sku, string nombre, int disponible, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/inventory/products", new { sku, nombre, disponible }, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
