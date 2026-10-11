using LearningPlatform.BuildingBlocks.Storage;
using LearningPlatform.Learning.Storage;

namespace LearningPlatform.Learning.Infrastructure.Storage;

internal sealed class LearningResourceStorage(IObjectStorage storage) : ILearningResourceStorage
{
    public Task PutAsync(Guid organizationId, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default) =>
        storage.PutAsync(organizationId, storageKey, content, contentType, cancellationToken);

    public Task<string?> GetReadUrlAsync(Guid organizationId, string storageKey,
        CancellationToken cancellationToken = default) =>
        storage.GetReadUrlAsync(organizationId, storageKey, cancellationToken);
}
