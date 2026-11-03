using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class ServiceEndpoints
{
    private static readonly string[] AllowedSorts = ["newest", "priceAsc", "priceDesc"];

    public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/services", ListServices);
        app.MapGet("/api/services/{id:int}", GetService);
        app.MapGet("/api/services/{id:int}/reviews", ListReviews);
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
        var errors = ValidatePaging(page, pageSize);
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
            items, page, pageSize, totalItems, TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> GetService(int id, MarketplaceDbContext db)
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

        if (service is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service not found");

        return Results.Ok(service with { AverageRating = RoundRating(service.AverageRating) });
    }

    private static async Task<IResult> ListReviews(
        int id,
        MarketplaceDbContext db,
        int page = 1,
        int pageSize = 10)
    {
        var errors = ValidatePaging(page, pageSize);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var exists = await db.Services.AnyAsync(s => s.Id == id && s.IsActive && s.Seller.IsActive);
        if (!exists)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service not found");

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
            items, page, pageSize, totalItems, TotalPages(totalItems, pageSize)));
    }

    private static Dictionary<string, string[]> ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1 || page > 100_000)
            errors["page"] = ["page must be between 1 and 100000"];

        if (pageSize < 1 || pageSize > 50)
            errors["pageSize"] = ["pageSize must be between 1 and 50"];

        return errors;
    }

    private static int TotalPages(int totalItems, int pageSize) =>
        (int)Math.Ceiling(totalItems / (double)pageSize);

    private static double? RoundRating(double? rating) =>
        rating is null ? null : Math.Round(rating.Value, 1);
}