using System.Security.Claims;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // The Admin policy is set once on the group, so every endpoint added here is protected by default.
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Admin);

        group.MapGet("/users", ListUsers);
        group.MapPost("/users/{id:int}/deactivate", DeactivateUser);
        group.MapPost("/users/{id:int}/activate", (int id, MarketplaceDbContext db) =>
            SetUserActive(id, true, db));

        group.MapGet("/services", ListServices);
        group.MapPost("/services/{id:int}/deactivate", (int id, MarketplaceDbContext db) =>
            SetServiceActive(id, false, db));
        group.MapPost("/services/{id:int}/activate", (int id, MarketplaceDbContext db) =>
            SetServiceActive(id, true, db));
    }

    private static async Task<IResult> ListUsers(MarketplaceDbContext db, int page = 1, int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var query = db.Users.AsNoTracking().OrderBy(u => u.Id);

        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserItem(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive))
            .ToListAsync();

        return Results.Ok(new PagedResult<AdminUserItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> ListServices(MarketplaceDbContext db, int page = 1, int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // Unlike the public list, this one includes inactive services.
        var query = db.Services.AsNoTracking().OrderBy(s => s.Id);

        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminServiceItem(s.Id, s.Title, s.Seller.DisplayName, s.Price, s.IsActive))
            .ToListAsync();

        return Results.Ok(new PagedResult<AdminServiceItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> DeactivateUser(int id, ClaimsPrincipal admin, MarketplaceDbContext db)
    {
        // Otherwise the only admin could lock everyone out by deactivating their own account.
        if (id == admin.GetUserId())
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "An admin cannot deactivate their own account.");
        }

        return await SetUserActive(id, false, db);
    }

    private static async Task<IResult> SetUserActive(int id, bool isActive, MarketplaceDbContext db)
    {
        var changed = await db.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive));

        return changed == 0
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found")
            : Results.NoContent();
    }

    private static async Task<IResult> SetServiceActive(int id, bool isActive, MarketplaceDbContext db)
    {
        var now = DateTime.UtcNow;

        var changed = await db.Services
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, isActive)
                .SetProperty(x => x.UpdatedAt, now));

        return changed == 0
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service not found")
            : Results.NoContent();
    }
}