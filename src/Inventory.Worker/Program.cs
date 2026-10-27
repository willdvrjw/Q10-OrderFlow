using Inventory.Worker.Application;
using Inventory.Worker.Endpoints;
using Inventory.Worker.Infrastructure.Messaging;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;

// Docker invoca esta ruta liviana para comprobar HTTP y PostgreSQL reales.
// No inicia otro servidor ni ejecuta operaciones de negocio.
if (args.Length == 1 && args[0] == "--healthcheck")
{
    using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await healthClient.GetAsync("http://127.0.0.1:8081/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception)
    {
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Inventory.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<OrderCreatedHandler>();
builder.Services.AddScoped<InventoryManagementService>();
builder.Services.AddScoped<InventoryProductRegistrationService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IInventoryChangeNotifier, SignalRInventoryChangeNotifier>();

// No se codifican credenciales; Compose suministra estas variables.
// Si faltan, el servicio falla explícitamente antes de recibir mensajes.
var rabbitUser = builder.Configuration["RabbitMq:UserName"]
    ?? throw new InvalidOperationException("Falta RabbitMq__UserName.");
var rabbitPassword = builder.Configuration["RabbitMq:Password"]
    ?? throw new InvalidOperationException("Falta RabbitMq__Password.");
var port = int.TryParse(builder.Configuration["RabbitMq:Port"], out var configuredPort)
    ? configuredPort : 5672;

builder.Services.AddSingleton(new RabbitMqSettings(
    builder.Configuration["RabbitMq:HostName"] ?? "rabbitmq",
    rabbitUser, rabbitPassword, port));

builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<InventoryOutboxDispatcher>();

var app = builder.Build();

// Los ajustes de inventario y las reservas comparten esta misma base propia.
// La semilla se prepara antes de servir HTTP o consumir eventos.
await InventoryDatabaseInitializer.InitializeAsync(
    app.Services, app.Logger, app.Lifetime.ApplicationStopping);

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (InventoryDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapInventoryEndpoints();
app.MapHub<InventoryUpdatesHub>("/hubs/inventory");

await app.RunAsync();
