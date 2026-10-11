using Marten;

namespace LearningPlatform.BuildingBlocks.Persistence;

public static class OrganizationSession
{
    public static IDocumentSession Open(IDocumentStore store, ITenantContext tenant)
    {
        return store.LightweightSession(tenant.TenantId);
    }
}
