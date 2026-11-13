using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Marketplace.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    // The signing key is a secret. It comes from user-secrets in Development and from an environment variable elsewhere.
    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    [Required]
    public string Issuer { get; set; } = "mini-marketplace-api";

    [Required]
    public string Audience { get; set; } = "mini-marketplace-clients";

    [Range(1, 1440)]
    public int ExpiresMinutes { get; set; } = 60;
}

public record IssuedToken(string Token, DateTime ExpiresAtUtc);

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        // The token is signed, not encrypted: anyone can read these claims, so they hold no secrets.
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", user.DisplayName),
                new Claim("role", user.Role.ToString())
            }),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expires);
    }
}