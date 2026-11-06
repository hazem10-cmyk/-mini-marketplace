using Marketplace.Api.Data;

namespace Marketplace.Api.Dtos;

public record CreateOrderRequest(int? ServiceId);

public record PartyInfo(int Id, string DisplayName);

public record ReviewInfo(int Rating, string? Comment, DateTime CreatedAt);

public record OrderListItem(
    int Id,
    string ServiceTitle,
    string OtherPartyName,
    decimal PriceAtOrder,
    OrderStatus Status,
    DateTime CreatedAt);

public record OrderDetails(
    int Id,
    int ServiceId,
    string ServiceTitle,
    PartyInfo Buyer,
    PartyInfo Seller,
    decimal PriceAtOrder,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ReviewInfo? Review);