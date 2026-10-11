using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace LearningPlatform.BuildingBlocks.Persistence;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid OrganizationId => Actor.OrganizationId;

    public string TenantId => OrganizationId.ToString("D");

    private AuthenticatedActor Actor
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal is null || !AuthenticatedActor.TryCreate(principal, out var actor))
            {
                throw new UnauthorizedAccessException("An authenticated organization is required.");
            }

            return actor!;
        }
    }
}
