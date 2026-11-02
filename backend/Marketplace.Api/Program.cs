using Marketplace.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    await db.Database.MigrateAsync();
    await DevSeeder.SeedAsync(db);
}

// Serve wwwroot/index.html at "/"
app.UseDefaultFiles();
app.UseStaticFiles();

// Temporary in-memory data (replaced by the database in step 2.6)
var services = new List<ServiceDto>
{
    new(1, "Professional logo design", 25m),
    new(2, "Article translation (up to 500 words)", 10m),
    new(3, "Landing page development", 60m),
};

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

app.MapGet("/api/services", (decimal? maxPrice) =>
{
    IEnumerable<ServiceDto> result = services;

    if (maxPrice is not null)
        result = result.Where(s => s.Price <= maxPrice);

    return Results.Ok(result);
});

app.MapGet("/api/services/{id:int}", (int id) =>
{
    var service = services.FirstOrDefault(s => s.Id == id);
    return service is null
        ? Results.NotFound(new { error = "Service not found" })
        : Results.Ok(service);
});

app.Run();

record ServiceDto(int Id, string Title, decimal Price);