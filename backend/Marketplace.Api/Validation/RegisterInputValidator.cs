using System.Net.Mail;
using Marketplace.Api.Data;
using Marketplace.Api.Dtos;

namespace Marketplace.Api.Validation;

public static class RegisterInputValidator
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    // Emails are stored trimmed and in lower case, so Ali@x.com and ali@x.com are the same account.
    public static string NormalizeEmail(string? email) =>
        (email ?? "").Trim().ToLowerInvariant();

    public static bool TryParseRole(string? role, out UserRole parsed)
    {
        // Only these two names are accepted: not Admin, and not numbers such as "1".
        var value = role?.Trim() ?? "";

        if (value.Equals(nameof(UserRole.Buyer), StringComparison.OrdinalIgnoreCase))
        {
            parsed = UserRole.Buyer;
            return true;
        }

        if (value.Equals(nameof(UserRole.Seller), StringComparison.OrdinalIgnoreCase))
        {
            parsed = UserRole.Seller;
            return true;
        }

        parsed = default;
        return false;
    }

    public static Dictionary<string, string[]> Validate(RegisterRequest input)
    {
        var errors = new Dictionary<string, string[]>();

        if (!IsValidEmail(NormalizeEmail(input.Email)))
            errors["email"] = ["email must be a valid address of up to 254 characters"];

        var password = input.Password ?? "";
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            errors["password"] = [$"password must be {MinPasswordLength} to {MaxPasswordLength} characters"];

        var name = input.DisplayName?.Trim() ?? "";
        if (name.Length < 2 || name.Length > 50)
            errors["displayName"] = ["displayName must be 2 to 50 characters"];

        if (!TryParseRole(input.Role, out _))
            errors["role"] = ["role must be Buyer or Seller"];

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length == 0 || email.Length > 254)
            return false;

        // The address must be exactly what was typed: no display name such as "Ali <ali@x.com>".
        return MailAddress.TryCreate(email, out var address)
            && address.Address == email
            && address.Host.Contains('.');
    }
}