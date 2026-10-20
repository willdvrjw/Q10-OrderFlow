using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;
using Orders.Api.Application.Orders;
using Orders.Api.Endpoints;
using Orders.Api.Infrastructure.Messaging;
using Orders.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();

var ordersConnection = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__Orders.");
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(ordersConnection));

builder.Services.AddSingleton(ProductCatalog.Load(
    Path.Combine(AppContext.BaseDirectory, "seed", "products.json")));
builder.Services.AddScoped<StockResultHandler>();

var rabbitUser = builder.Configuration["RabbitMq:UserName"]
    ?? throw new InvalidOperationException("Falta RabbitMq__UserName.");
var rabbitPassword = builder.Configuration["RabbitMq:Password"]
    ?? throw new InvalidOperationException("Falta RabbitMq__Password.");
var port = int.TryParse(builder.Configuration["RabbitMq:Port"], out var configuredPort)
    ? configuredPort : 5672;

builder.Services.AddSingleton(new RabbitMqSettings(
    builder.Configuration["RabbitMq:HostName"] ?? "rabbitmq",
    rabbitUser, rabbitPassword, port));

builder.Services.AddHostedService<OrdersOutboxDispatcher>();
builder.Services.AddHostedService<StockResultConsumer>();

var app = builder.Build();

// No exponer HTTP hasta que el almacenamiento de pedidos esté preparado.
// RabbitMQ, en cambio, puede estar caído: las outbox reintentan sin perder pedidos.
await OrdersDatabaseInitializer.InitializeAsync(
    app.Services, app.Logger, app.Lifetime.ApplicationStopping);

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (OrdersDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapOrdersEndpoints();
app.MapHub<OrdersHub>("/hubs/orders");
app.Run();
