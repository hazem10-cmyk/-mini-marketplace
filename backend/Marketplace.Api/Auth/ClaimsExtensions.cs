using System.Security.Claims;

namespace Marketplace.Api.Auth;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}