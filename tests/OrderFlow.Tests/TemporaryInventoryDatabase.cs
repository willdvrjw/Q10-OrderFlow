using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OrderFlow.Tests;

/// <summary>
/// Crea una base PostgreSQL aislada por prueba de integración, de manera que
/// los experimentos de concurrencia no modifiquen el inventario de demostración.
/// Requiere ORDERFLOW_TEST_POSTGRES con conexión a la base administrativa "postgres".
/// Su nombre se genera exclusivamente con un GUID (identificador SQL seguro).
/// </summary>
internal sealed class TemporaryInventoryDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _name;
    private readonly string _testConnectionString;

    private TemporaryInventoryDatabase(
        string adminConnectionString,
        string name,
        string testConnectionString)
    {
        _adminConnectionString = adminConnectionString;
        _name = name;
        _testConnectionString = testConnectionString;
    }

    /// <summary>
    /// Crea una base separada y genera su esquema mediante el mismo modelo EF Core
    /// empleado por Inventory. No utiliza mocks ni SQLite para simular PostgreSQL.
    /// </summary>
    public static async Task<TemporaryInventoryDatabase> CreateAsync()
    {
        var configured = Environment.GetEnvironmentVariable("ORDERFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Define ORDERFLOW_TEST_POSTGRES o ejecuta: " +
                "docker compose --profile tests run --build --rm backend-tests");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(configured)
        {
            Database = "postgres",
            Pooling = false
        };

        var databaseName = "orderflow_test_" + Guid.NewGuid().ToString("N");
        var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = databaseName
        };

        // El identificador procede solo de los dígitos hexadecimales de Guid.
        // No incluye ningún dato introducido por el usuario.
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var result = new TemporaryInventoryDatabase(
            adminBuilder.ConnectionString, databaseName, testBuilder.ConnectionString);

        try
        {
            await using var context = result.OpenContext();
            await context.Database.EnsureCreatedAsync();
            return result;
        }
        catch
        {
            await result.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Cada consumidor abre su propia conexión/DbContext. Esto es necesario para
    /// comprobar de verdad el bloqueo y la actualización atómica concurrente.
    /// </summary>
    public InventoryDbContext OpenContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_testConnectionString)
            .Options;
        return new InventoryDbContext(options);
    }

    /// <summary>
    /// Destruye únicamente la base temporal generada para este test.
    /// FORCE cierra conexiones residuales de esa base sin afectar otras bases.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
