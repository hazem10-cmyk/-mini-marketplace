using System.Security.Claims;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Marketplace.Api.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register);
        group.MapGet("/me", GetMe).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        IPasswordHasher<User> hasher,
        MarketplaceDbContext db)
    {
        var errors = RegisterInputValidator.Validate(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = RegisterInputValidator.NormalizeEmail(request.Email);
        RegisterInputValidator.TryParseRole(request.Role, out var role);

        if (await db.Users.AnyAsync(u => u.Email == email))
            return EmailTaken();

        // Only Buyer or Seller is possible here, and the account is active (decision D12).
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName!.Trim(),
            Role = role
        };

        // The hash holds a random salt and the iteration count. The password itself is never stored.
        user.PasswordHash = hasher.HashPassword(user, request.Password!);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Two registrations with the same email arrived together: the unique index let only one in.
            return EmailTaken();
        }

        return Results.Json(
            new CurrentUser(user.Id, user.Email, user.DisplayName, user.Role),
            statusCode: StatusCodes.Status201Created);
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

    private static IResult EmailTaken() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: "This email is already registered.");
}