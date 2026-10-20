using Microsoft.EntityFrameworkCore;
using Orders.Api.Application.Orders;
using Orders.Api.Endpoints;
using Orders.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

// Se configura mediante ConnectionStrings__Orders en Compose.
// Un valor ausente detiene el arranque en vez de ocultar un error de configuración.
var ordersConnection = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException(
        "Falta la variable de entorno ConnectionStrings__Orders.");

builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(ordersConnection));
builder.Services.AddSingleton(ProductCatalog.Load(
    Path.Combine(AppContext.BaseDirectory, "seed", "products.json")));

var app = builder.Build();

// Intencionalmente se inicializa antes de escuchar HTTP para no exponer una API
// aparentemente lista cuyo esquema PostgreSQL todavía no existe.
await OrdersDatabaseInitializer.InitializeAsync(
    app.Services, app.Logger, app.Lifetime.ApplicationStopping);

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (OrdersDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapOrdersEndpoints();
app.Run();
