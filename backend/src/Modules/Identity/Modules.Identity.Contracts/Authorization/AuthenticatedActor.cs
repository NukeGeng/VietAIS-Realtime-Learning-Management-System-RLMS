using System.Security.Claims;

namespace LearningPlatform.Identity.Authorization;

public sealed record AuthenticatedActor(Guid UserId, Guid OrganizationId, string[] Permissions)
{
    public const string OrganizationClaim = "organization_id";
    public const string PermissionClaim = "permission";

    public static bool TryCreate(ClaimsPrincipal principal, out AuthenticatedActor? actor)
    {
        actor = null;
        if (principal.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(OrganizationClaim), out var organizationId) ||
            userId == Guid.Empty || organizationId == Guid.Empty)
        {
            return false;
        }

        actor = new(userId, organizationId,
            principal.FindAll(PermissionClaim).Select(c => c.Value).Distinct().ToArray());
        return true;
    }
}
