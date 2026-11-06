using System.Security.Claims;
using Marketplace.Api.Auth;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", CreateOrder).RequireAuthorization(Policies.Buyer);
        group.MapGet("/", ListOrders).RequireAuthorization(Policies.BuyerOrSeller);
        group.MapGet("/{id:int}", GetOrder).RequireAuthorization();
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        ClaimsPrincipal user,
        MarketplaceDbContext db)
    {
        if (request.ServiceId is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["serviceId"] = ["serviceId is required"]
            });
        }

        var serviceId = request.ServiceId.Value;

        var service = await db.Services
            .AsNoTracking()
            .Where(s => s.Id == serviceId && s.IsActive && s.Seller.IsActive)
            .Select(s => new { s.Id, s.Price })
            .FirstOrDefaultAsync();

        if (service is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service not found");

        var buyerId = user.GetUserId();

        // The price is copied from the service now and never changes (decision D3).
        var order = new Order
        {
            ServiceId = service.Id,
            BuyerId = buyerId,
            PriceAtOrder = service.Price
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var details = await GetDetailsAsync(db, order.Id, buyerId);
        return Results.Created($"/api/orders/{order.Id}", details);
    }

    private static async Task<IResult> ListOrders(
        ClaimsPrincipal user,
        MarketplaceDbContext db,
        string? status,
        int page = 1,
        int pageSize = 10)
    {
        var errors = Paging.Validate(page, pageSize);

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var name = Enum.GetNames<OrderStatus>()
                .FirstOrDefault(n => n.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));

            if (name is null)
                errors["status"] = [$"status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}"];
            else
                statusFilter = Enum.Parse<OrderStatus>(name);
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var userId = user.GetUserId();
        var isBuyer = user.IsInRole(nameof(UserRole.Buyer));

        // A buyer sees the orders they placed. A seller sees the orders of their services.
        IQueryable<Order> query = db.Orders.AsNoTracking();
        query = isBuyer
            ? query.Where(o => o.BuyerId == userId)
            : query.Where(o => o.Service.SellerId == userId);

        if (statusFilter is not null)
        {
            var wanted = statusFilter.Value;
            query = query.Where(o => o.Status == wanted);
        }

        query = query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id);

        var totalItems = await query.CountAsync();

        var paged = query.Skip((page - 1) * pageSize).Take(pageSize);

        var items = isBuyer
            ? await paged.Select(o => new OrderListItem(
                    o.Id,
                    o.Service.Title,
                    o.Service.Seller.DisplayName,
                    o.PriceAtOrder,
                    o.Status,
                    o.CreatedAt))
                .ToListAsync()
            : await paged.Select(o => new OrderListItem(
                    o.Id,
                    o.Service.Title,
                    o.Buyer.DisplayName,
                    o.PriceAtOrder,
                    o.Status,
                    o.CreatedAt))
                .ToListAsync();

        return Results.Ok(new PagedResult<OrderListItem>(
            items, page, pageSize, totalItems, Paging.TotalPages(totalItems, pageSize)));
    }

    private static async Task<IResult> GetOrder(int id, ClaimsPrincipal user, MarketplaceDbContext db)
    {
        var order = await GetDetailsAsync(db, id, user.GetUserId());

        // Missing, or not part of this order: the same answer, so we do not reveal that it exists (decision D10).
        return order is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found")
            : Results.Ok(order);
    }

    private static async Task<OrderDetails?> GetDetailsAsync(MarketplaceDbContext db, int orderId, int userId)
    {
        return await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId && (o.BuyerId == userId || o.Service.SellerId == userId))
            .Select(o => new OrderDetails(
                o.Id,
                o.ServiceId,
                o.Service.Title,
                new PartyInfo(o.Buyer.Id, o.Buyer.DisplayName),
                new PartyInfo(o.Service.Seller.Id, o.Service.Seller.DisplayName),
                o.PriceAtOrder,
                o.Status,
                o.CreatedAt,
                o.UpdatedAt,
                o.Review == null
                    ? null
                    : new ReviewInfo(o.Review.Rating, o.Review.Comment, o.Review.CreatedAt)))
            .FirstOrDefaultAsync();
    }
}