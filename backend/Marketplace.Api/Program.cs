using System.Text.Json.Serialization;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var authentication = builder.Services.AddAuthentication(DevHeaderAuthHandler.SchemeName);
if (builder.Environment.IsDevelopment())
{
    authentication.AddScheme<AuthenticationSchemeOptions, DevHeaderAuthHandler>(
        DevHeaderAuthHandler.SchemeName, null);
}
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await db.Database.MigrateAsync();
        await DevSeeder.SeedAsync(db);
    }

    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Serve wwwroot/index.html at "/"
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));
app.MapServiceEndpoints();
app.MapAuthEndpoints();

app.Run();