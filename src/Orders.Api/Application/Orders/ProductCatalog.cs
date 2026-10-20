using System.Text.Json;

namespace Orders.Api.Application.Orders;

/// <summary>
/// Catálogo estático de demostración para validar que el SKU exista.
/// NO indica cuánto stock queda; esa responsabilidad pertenece a Inventory Worker.
/// Ambos servicios cargarán el mismo archivo versionado de productos iniciales.
/// </summary>
public sealed class ProductCatalog
{
    private readonly HashSet<string> _skus;
    private readonly IReadOnlyList<ProductDefinition> _products;

    private ProductCatalog(HashSet<string> skus, IReadOnlyList<ProductDefinition> products)
    {
        _skus = skus;
        _products = products;
    }

    public IReadOnlyCollection<string> Skus => _skus;
    public IReadOnlyList<ProductDefinition> Products => _products;

    public bool Contains(string? sku) =>
        !string.IsNullOrWhiteSpace(sku) && _skus.Contains(sku.Trim());

    /// <summary>
    /// Falla al arrancar si la semilla está incompleta o duplicada, evitando que
    /// Orders acepte un conjunto de SKU distinto al inicializado en Inventory.
    /// </summary>
    public static ProductCatalog Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("No se encontró el catálogo inicial.", path);

        var products = JsonSerializer.Deserialize<List<ProductDefinition>>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("El catálogo está vacío.");

        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.Sku) ||
                string.IsNullOrWhiteSpace(product.Nombre) ||
                product.Disponible < 0 ||
                !skus.Add(product.Sku.Trim()))
            {
                throw new InvalidDataException("Catálogo con SKU duplicado, datos incompletos o stock negativo.");
            }
        }

        if (skus.Count < 3)
            throw new InvalidDataException("La prueba requiere al menos tres productos en el catálogo.");

        return new ProductCatalog(skus, products.AsReadOnly());
    }
}

/// <summary>Definición de cada SKU en el catálogo de demostración.</summary>
public sealed record ProductDefinition(string Sku, string Nombre, int Disponible);
