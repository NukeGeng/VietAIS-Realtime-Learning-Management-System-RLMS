namespace LearningPlatform.BuildingBlocks.Storage;

public interface IObjectStorage
{
    Task PutAsync(Guid organizationId, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);
    Task<string?> GetReadUrlAsync(Guid organizationId, string storageKey,
        CancellationToken cancellationToken = default);
}
