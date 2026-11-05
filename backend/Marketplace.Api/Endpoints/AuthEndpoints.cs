using System.Security.Claims;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", GetMe)
            .RequireAuthorization()
            .WithTags("Auth");
    }

    private static async Task<IResult> GetMe(ClaimsPrincipal principal, MarketplaceDbContext db)
    {
        var id = principal.GetUserId();

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new CurrentUser(u.Id, u.Email, u.DisplayName, u.Role))
            .FirstOrDefaultAsync();

        return user is null ? Results.Unauthorized() : Results.Ok(user);
    }
}