using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PetProductivity.Server.Services;

public static class ClaimsPrincipalExtensions
{
    // El userId autenticado sale SIEMPRE del token, nunca del body/query.
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? user.FindFirst("sub")?.Value
              ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }
}
