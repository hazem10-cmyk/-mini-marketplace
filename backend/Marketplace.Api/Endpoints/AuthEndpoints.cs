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
        group.MapPost("/login", Login);
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

    private static async Task<IResult> Login(
        LoginRequest request,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        MarketplaceDbContext db)
    {
        var email = RegisterInputValidator.NormalizeEmail(request.Email);
        var password = request.Password ?? "";

        var errors = new Dictionary<string, string[]>();
        if (email.Length == 0)
            errors["email"] = ["email is required"];
        if (password.Length == 0)
            errors["password"] = ["password is required"];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // Registration refuses values this long, so they cannot be real. We do not spend time hashing them.
        if (email.Length > 254 || password.Length > RegisterInputValidator.MaxPasswordLength)
            return InvalidCredentials();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            // Spend about the same time as a real check, so the response time does not reveal that the email is unknown.
            hasher.HashPassword(new User(), password);
            return InvalidCredentials();
        }

        var verification = Verify(hasher, user, password);
        if (verification == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        // Only someone who knows the password learns that the account is deactivated.
        if (!user.IsActive)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account deactivated",
                detail: "This account is deactivated.");
        }

        // The stored hash was made with fewer iterations than today's setting: store a stronger one now.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        var issued = tokens.Issue(user);

        return Results.Ok(new LoginResponse(
            issued.Token,
            issued.ExpiresAtUtc,
            new CurrentUser(user.Id, user.Email, user.DisplayName, user.Role)));
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

    private static PasswordVerificationResult Verify(IPasswordHasher<User> hasher, User user, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (FormatException)
        {
            // A stored value that is not a valid hash (for example an old placeholder) never matches.
            return PasswordVerificationResult.Failed;
        }
    }

    private static IResult EmailTaken() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: "This email is already registered.");

    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "Wrong email or password.");
}