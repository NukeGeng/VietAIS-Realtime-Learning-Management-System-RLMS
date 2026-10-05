using Marten;

namespace LearningPlatform.Infrastructure.Persistence;

public static class OrganizationSession
{
    public static IDocumentSession Open(IDocumentStore store, ITenantContext tenant)
    {
        return store.LightweightSession(tenant.TenantId);
    }
}
