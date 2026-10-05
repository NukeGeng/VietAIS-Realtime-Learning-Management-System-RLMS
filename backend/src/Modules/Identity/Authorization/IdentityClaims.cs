using System.Security.Claims;
using LearningPlatform.Identity.Domain;

namespace LearningPlatform.Identity.Authorization;

public static class IdentityClaims
{
    public const string SecurityStampClaim = "security_stamp";

    public static ClaimsPrincipal Create(UserAccount account, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString("D")),
            new(AuthenticatedActor.OrganizationClaim, account.OrganizationId.ToString("D")),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Name, account.DisplayName),
            new(SecurityStampClaim, account.SecurityStamp)
        };
        claims.AddRange(permissions.Distinct(StringComparer.Ordinal)
            .Select(permission => new Claim(AuthenticatedActor.PermissionClaim, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }
}
