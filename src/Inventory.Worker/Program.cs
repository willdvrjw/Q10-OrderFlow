using Inventory.Worker.Application;
using Inventory.Worker.Infrastructure.Messaging;
using Inventory.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Inventory.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<OrderCreatedHandler>();

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

using var host = builder.Build();

// La base y el seed deben estar listos antes de consumir pedidos;
// en caso de error de conexión inicial se reintenta temporalmente.
await InventoryDatabaseInitializer.InitializeAsync(
    host.Services,
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("InventoryStartup"),
    CancellationToken.None);

await host.RunAsync();
