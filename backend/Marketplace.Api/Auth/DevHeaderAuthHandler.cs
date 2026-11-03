using System.Security.Claims;
using System.Text.Encodings.Web;
using Marketplace.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Marketplace.Api.Auth;

// Development only: "logs in" as the user whose id is in the X-Dev-User header.
// It is registered only in the Development environment. Phase 4 replaces it with real login.
public class DevHeaderAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    MarketplaceDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "DevHeader";
    public const string HeaderName = "X-Dev-User";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var header))
            return AuthenticateResult.NoResult();

        if (!int.TryParse(header.ToString(), out var userId))
            return AuthenticateResult.Fail("X-Dev-User must be a number");

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user is null)
            return AuthenticateResult.Fail("Unknown or inactive user");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}