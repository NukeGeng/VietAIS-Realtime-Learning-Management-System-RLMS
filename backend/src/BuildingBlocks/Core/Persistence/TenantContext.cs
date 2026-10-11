namespace LearningPlatform.BuildingBlocks.Persistence;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    string TenantId { get; }
}
