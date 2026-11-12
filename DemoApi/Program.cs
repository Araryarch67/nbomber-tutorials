using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();

// Meniru pool resource terbatas, bukan CPU atau database sungguhan.
using var inventorySlots = new SemaphoreSlim(20, 20);
using var priceSlots = new SemaphoreSlim(20, 20);

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/catalog", async (HttpContext context) =>
{
    await Task.Delay(30, context.RequestAborted);
    return Results.Ok(new { ok = true, productId = 1, name = "Kopi" });
});

app.MapGet("/api/inventory", async (HttpContext context) =>
{
    if (!await inventorySlots.WaitAsync(500, context.RequestAborted))
        return Results.Json(new { ok = false, error = "inventory_busy" }, statusCode: 503);
    try
    {
        await Task.Delay(200, context.RequestAborted);
        return Results.Ok(new { ok = true, productId = 1, stock = 25 });
    }
    finally { inventorySlots.Release(); }
});

app.MapGet("/api/prices", async (HttpContext context) =>
{
    if (!await priceSlots.WaitAsync(500, context.RequestAborted))
        return Results.Json(new { ok = false, error = "price_busy" }, statusCode: 503);
    try
    {
        await Task.Delay(40, context.RequestAborted);
        return Results.Ok(new { ok = true, productId = 1, price = 25000 });
    }
    finally { priceSlots.Release(); }
});

app.MapGet("/api/search", async (HttpContext context) =>
{
    int bucket = Random.Shared.Next(100);
    int delayMs = bucket < 90 ? 40 : bucket < 98 ? 250 : 1200;
    await Task.Delay(delayMs, context.RequestAborted);
    return Results.Ok(new { ok = true, query = "kopi", count = 5 });
});

app.MapPost("/api/checkout", async (HttpContext context) =>
{
    string raw = context.Request.Query["failurePercent"].ToString();
    if (!int.TryParse(raw, out int percent) || percent < 0 || percent > 100)
        return Results.BadRequest(new { ok = false, error = "invalid_percentage" });
    await Task.Delay(80, context.RequestAborted);
    if (Random.Shared.Next(100) < percent)
        return Results.Json(new { ok = false, error = "payment_unavailable" }, statusCode: 503);
    return Results.Ok(new { ok = true, orderId = Guid.NewGuid() });
});

app.Run();
