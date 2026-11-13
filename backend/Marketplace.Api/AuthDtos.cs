namespace Marketplace.Api.Dtos;

public record RegisterRequest(string? Email, string? Password, string? DisplayName, string? Role);

public record LoginRequest(string? Email, string? Password);

public record LoginResponse(string Token, DateTime ExpiresAt, CurrentUser User);