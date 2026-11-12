using System.Security.Claims;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Marketplace.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class ServiceEndpoints
{
    private static readonly string[] AllowedSorts = ["newest", "priceAsc", "priceDesc"];

    public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services").WithTags("Services");

        group.MapGet("/", ListServices);
        group.MapGet("/{id:int}", GetService);
        group.MapGet("/{id:int}/reviews", ListReviews);

        group.MapGet("/mine", ListMyServices).RequireAuthorization(Policies.Seller);
        group.MapPost("/", CreateService).RequireAuthorization(Policies.Seller);
        group.MapPut("/{id:int}", UpdateService).RequireAuthorization(Policies.Seller);
        group.MapDelete("/{id:int}", DeleteService).RequireAuthorization(Policies.Seller);
    }

    private static async Task<IResult> ListServices(
        MarketplaceDbContext db,
        string? q,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page = 1,
        int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);
        if (sort is not null && !AllowedSorts.Contains(sort))
            errors["sort"] = ["sort must be newest, priceAsc, or priceDesc"];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var query = db.Services
            .AsNoTracking()
            .Where(s => s.IsActive && s.Seller.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Title.Contains(term) || s.Description.Contains(term));
        }

        if (minPrice is not null)
            query = query.Where(s => s.Price >= minPrice);

        if (maxPrice is not null)
            query = query.Where(s => s.Price <= maxPrice);

        query = sort switch
        {
            "priceAsc" => query.OrderBy(s => s.Price).ThenBy(s => s.Id),
            "priceDesc" => query.OrderByDescending(s => s.Price).ThenBy(s => s.Id),
            _ => query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
        };

        var totalItems = await query.CountAsync();

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ServiceListItem(
                s.Id,
                s.Title,
                s.Price,
                s.DeliveryDays,
                s.Seller.DisplayName,
                s.Orders.Where(o => o.Review != null).Average(o => (double?)o.Review!.Rating),
                s.Orders.Count(o => o.Review != null)))
            .ToListAsync();

        var items = rows
            .Select(r => r with { AverageRating = RoundRating(r.AverageRating) })
            .ToList();

        return Results.Ok(new PagedResult<ServiceListItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> GetService(int id, MarketplaceDbContext db)
    {
        var service = await GetDetailsAsync(db, id);
        return service is null ? ServiceNotFound() : Results.Ok(service);
    }

    private static async Task<IResult> ListReviews(
        int id,
        MarketplaceDbContext db,
        int page = 1,
        int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var exists = await db.Services.AnyAsync(s => s.Id == id && s.IsActive && s.Seller.IsActive);
        if (!exists)
            return ServiceNotFound();

        var query = db.Reviews
            .AsNoTracking()
            .Where(r => r.Order.ServiceId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id);

        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewItem(r.Rating, r.Comment, r.Order.Buyer.DisplayName, r.CreatedAt))
            .ToListAsync();

        return Results.Ok(new PagedResult<ReviewItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> ListMyServices(
        ClaimsPrincipal user,
        MarketplaceDbContext db,
        int page = 1,
        int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var sellerId = user.GetUserId();

        var query = db.Services
            .AsNoTracking()
            .Where(s => s.SellerId == sellerId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id);

        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new MyServiceItem(s.Id, s.Title, s.Price, s.DeliveryDays, s.CreatedAt))
            .ToListAsync();

        return Results.Ok(new PagedResult<MyServiceItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> CreateService(
        ServiceInput input,
        ClaimsPrincipal user,
        MarketplaceDbContext db)
    {
        var errors = ServiceInputValidator.Validate(input);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var service = new Service
        {
            SellerId = user.GetUserId(),
            Title = input.Title!.Trim(),
            Description = input.Description!.Trim(),
            Price = input.Price,
            DeliveryDays = input.DeliveryDays
        };

        db.Services.Add(service);
        await db.SaveChangesAsync();

        var details = await GetDetailsAsync(db, service.Id);
        return Results.Created($"/api/services/{service.Id}", details);
    }

    private static async Task<IResult> UpdateService(
        int id,
        ServiceInput input,
        ClaimsPrincipal user,
        MarketplaceDbContext db)
    {
        var errors = ServiceInputValidator.Validate(input);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (service is null)
            return ServiceNotFound();

        if (service.SellerId != user.GetUserId())
            return NotOwner();

        service.Title = input.Title!.Trim();
        service.Description = input.Description!.Trim();
        service.Price = input.Price;
        service.DeliveryDays = input.DeliveryDays;
        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(await GetDetailsAsync(db, id));
    }

    private static async Task<IResult> DeleteService(
        int id,
        ClaimsPrincipal user,
        MarketplaceDbContext db)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (service is null)
            return ServiceNotFound();

        if (service.SellerId != user.GetUserId())
            return NotOwner();

        service.IsActive = false;
        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<ServiceDetails?> GetDetailsAsync(MarketplaceDbContext db, int id)
    {
        var service = await db.Services
            .AsNoTracking()
            .Where(s => s.Id == id && s.IsActive && s.Seller.IsActive)
            .Select(s => new ServiceDetails(
                s.Id,
                s.Title,
                s.Description,
                s.Price,
                s.DeliveryDays,
                new SellerInfo(s.Seller.Id, s.Seller.DisplayName),
                s.Orders.Where(o => o.Review != null).Average(o => (double?)o.Review!.Rating),
                s.Orders.Count(o => o.Review != null)))
            .FirstOrDefaultAsync();

        return service is null
            ? null
            : service with { AverageRating = RoundRating(service.AverageRating) };
    }

    private static IResult ServiceNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service not found");

    private static IResult NotOwner() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "You can only change your own services");

    private static double? RoundRating(double? rating) =>
        rating is null ? null : Math.Round(rating.Value, 1);
}