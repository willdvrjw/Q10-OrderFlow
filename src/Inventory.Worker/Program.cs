using Inventory.Worker.Application;
using Inventory.Worker.Endpoints;
using Inventory.Worker.Infrastructure.Messaging;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Inventory.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<OrderCreatedHandler>();
builder.Services.AddScoped<InventoryManagementService>();

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

await app.RunAsync();
