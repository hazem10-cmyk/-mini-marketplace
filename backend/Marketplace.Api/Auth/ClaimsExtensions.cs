using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Marketplace.Api.Auth;

public static class ClaimsExtensions
{
    // The user id is the "sub" claim of the token.
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}