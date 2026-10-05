using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Http;

namespace LearningPlatform.Infrastructure.Persistence;

public static class OrganizationSession
{
    public static IDocumentSession Open(IDocumentStore store, IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        if (principal is null || !AuthenticatedActor.TryCreate(principal, out var actor))
        {
            throw new UnauthorizedAccessException("An authenticated organization is required.");
        }

        return store.LightweightSession(actor!.OrganizationId.ToString("D"));
    }
}
