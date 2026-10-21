namespace Orders.Api.Infrastructure.Persistence;

/// <summary>
/// Productos añadidos desde el panel y persistidos por Orders.
/// Los tres SKU de demostración siguen viniendo del archivo seed.
/// Inventory mantiene en su propia base el saldo de todos los SKU.
/// </summary>
public sealed class CatalogProduct
{
    private CatalogProduct() { }

    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;

    public static CatalogProduct Create(string sku, string nombre)
    {
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > 64 ||
            string.IsNullOrWhiteSpace(nombre) || nombre.Length > 160)
            throw new ArgumentException("Los datos del producto son inválidos.");

        return new CatalogProduct
        {
            Sku = sku.Trim().ToUpperInvariant(),
            Nombre = nombre.Trim()
        };
    }
}
