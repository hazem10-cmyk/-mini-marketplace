using System.Text;
using Marketplace.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddMarketplaceAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // The rules come from JwtOptions: the same validated settings the token service uses to sign tokens.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the claim names exactly as they are in the token (sub, name, role) instead of renaming them.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    // Only the algorithm we sign with is accepted. A token with "alg": "none" is refused.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    // The default tolerance is 5 minutes. A token should stop working when it expires.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = CheckUserIsStillActive
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Buyer, policy => policy.RequireRole(nameof(UserRole.Buyer)))
            .AddPolicy(Policies.Seller, policy => policy.RequireRole(nameof(UserRole.Seller)))
            .AddPolicy(Policies.BuyerOrSeller, policy => policy.RequireRole(nameof(UserRole.Buyer), nameof(UserRole.Seller)))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)));

        return services;
    }

    // A valid signature is not enough: the user must still exist and be active.
    // Without this check, a deactivated user's old token would keep working until it expires.
    private static async Task CheckUserIsStillActive(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!int.TryParse(subject, out var userId))
        {
            context.Fail("The token has no valid subject.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var isActive = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive);

        if (!isActive)
            context.Fail("The user does not exist or is deactivated.");
    }
}