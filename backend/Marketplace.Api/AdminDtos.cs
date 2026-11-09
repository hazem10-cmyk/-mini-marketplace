using Marketplace.Api.Data;

namespace Marketplace.Api.Dtos;

public record AdminUserItem(int Id, string Email, string DisplayName, UserRole Role, bool IsActive);

public record AdminServiceItem(int Id, string Title, string SellerName, decimal Price, bool IsActive);