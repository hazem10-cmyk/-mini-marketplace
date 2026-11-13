using System.Text.Json.Serialization;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Password hashing: PBKDF2 with HMAC-SHA512 and a random salt. 210,000 iterations is the OWASP figure for SHA-512.
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

var authentication = builder.Services.AddAuthentication(DevHeaderAuthHandler.SchemeName);
if (builder.Environment.IsDevelopment())
{
    authentication.AddScheme<AuthenticationSchemeOptions, DevHeaderAuthHandler>(
        DevHeaderAuthHandler.SchemeName, null);
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Buyer, policy => policy.RequireRole(nameof(UserRole.Buyer)))
    .AddPolicy(Policies.Seller, policy => policy.RequireRole(nameof(UserRole.Seller)))
    .AddPolicy(Policies.BuyerOrSeller, policy => policy.RequireRole(nameof(UserRole.Buyer), nameof(UserRole.Seller)))
    .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)));

var app = builder.Build();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await db.Database.MigrateAsync();
        await DevSeeder.SeedAsync(db);
        await DevPasswords.ApplyAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
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
app.MapOrderEndpoints();
app.MapAdminEndpoints();

app.Run();