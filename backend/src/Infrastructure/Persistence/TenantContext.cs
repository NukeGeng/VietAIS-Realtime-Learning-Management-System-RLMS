using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace LearningPlatform.Infrastructure.Persistence;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    string TenantId { get; }
}

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
