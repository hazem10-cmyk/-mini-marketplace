namespace Marketplace.Api.Dtos;

public record RegisterRequest(string? Email, string? Password, string? DisplayName, string? Role);