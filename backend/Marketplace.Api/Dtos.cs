using Marketplace.Api.Data;

namespace Marketplace.Api.Dtos;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public record ServiceListItem(
    int Id,
    string Title,
    decimal Price,
    int DeliveryDays,
    string SellerName,
    double? AverageRating,
    int ReviewCount);

public record SellerInfo(int Id, string DisplayName);

public record ServiceDetails(
    int Id,
    string Title,
    string Description,
    decimal Price,
    int DeliveryDays,
    SellerInfo Seller,
    double? AverageRating,
    int ReviewCount);

public record ReviewItem(
    int Rating,
    string? Comment,
    string BuyerName,
    DateTime CreatedAt);

public record CurrentUser(int Id, string Email, string DisplayName, UserRole Role);