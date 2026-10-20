using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Infrastructure.Persistence;

/// <summary>
/// Prepara automáticamente la base de demostración para que Compose no exija
/// ejecutar comandos manuales. Se reintenta cuando PostgreSQL aún está arrancando.
/// EnsureCreated es una simplificación explícita de la prueba; en producción
/// usaríamos migraciones versionadas, no EnsureCreated junto con Migrate.
/// </summary>
public static class OrdersDatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 15;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                await db.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("Base de Orders lista en intento {Attempt}.", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex, "PostgreSQL no está listo. Reintento {Attempt}/{MaxAttempts}.",
                    attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }
}
