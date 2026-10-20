using Orders.Api.Application.Orders;
using Orders.Api.Domain;

namespace OrderFlow.Tests;

/// <summary>
/// Primeros tres tests reales de dominio.
/// Aún no demuestran persistencia, RabbitMQ ni concurrencia: esas pruebas
/// se incorporarán cuando estén implementados el repositorio y el worker.
/// </summary>
public sealed class OrderBusinessRulesTests
{
    private static readonly string[] CatalogSkus = ["ABC-01", "XYZ-02", "TEST-03"];
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidationRejectsInvalidCustomerSkuAndQuantity()
    {
        var withoutName = OrderValidator.Validate(
            new CreateOrderRequest("   ", "ABC-01", 2), CatalogSkus);
        Assert.Contains("clienteNombre", withoutName.Keys);

        var nonexistentSku = OrderValidator.Validate(
            new CreateOrderRequest("María", "NO-EXISTE", 2), CatalogSkus);
        Assert.Contains("sku", nonexistentSku.Keys);

        var invalidQuantity = OrderValidator.Validate(
            new CreateOrderRequest("María", "ABC-01", 101), CatalogSkus);
        Assert.Contains("cantidad", invalidQuantity.Keys);

        // Con SKU válido, el validador no rechaza por falta de inventario.
        var valid = OrderValidator.Validate(
            new CreateOrderRequest("María", "abc-01", 2), CatalogSkus);
        Assert.Empty(valid);
    }

    [Fact]
    public void CreatedOrderStartsPendingAndConfirmsOnlyOnce()
    {
        var order = Order.Create(Guid.NewGuid(), "  María  ", "abc-01", 2, Now);

        Assert.Equal(OrderStatus.Pending, order.Estado);
        Assert.Equal("María", order.ClienteNombre);
        Assert.Equal("ABC-01", order.Sku);

        Assert.True(order.TryConfirm(Now.AddSeconds(1)));
        Assert.Equal(OrderStatus.Confirmed, order.Estado);

        // Repetición o resultado tardío contradictorio no cambia el estado.
        Assert.False(order.TryConfirm(Now.AddSeconds(2)));
        Assert.False(order.TryReject(Now.AddSeconds(3), "Sin stock"));
        Assert.Equal(OrderStatus.Confirmed, order.Estado);
    }

    [Fact]
    public void RejectedOrderCannotLaterBeConfirmed()
    {
        var order = Order.Create(Guid.NewGuid(), "María", "ABC-01", 3, Now);

        Assert.True(order.TryReject(Now.AddSeconds(1), "Sin existencias"));
        Assert.Equal(OrderStatus.Rejected, order.Estado);
        Assert.Equal("Sin existencias", order.MotivoRechazo);

        Assert.False(order.TryConfirm(Now.AddSeconds(2)));
        Assert.False(order.TryReject(Now.AddSeconds(3), "Otro resultado"));
        Assert.Equal("Sin existencias", order.MotivoRechazo);
    }
}
