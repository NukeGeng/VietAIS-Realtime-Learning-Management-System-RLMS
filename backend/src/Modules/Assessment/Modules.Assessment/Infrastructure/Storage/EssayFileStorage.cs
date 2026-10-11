using LearningPlatform.BuildingBlocks.Storage;
using LearningPlatform.Assessment.Storage;

namespace LearningPlatform.Assessment.Infrastructure.Storage;

internal sealed class EssayFileStorage(IObjectStorage storage) : IFileStorage
{
    public Task PutAsync(Guid organizationId, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default) =>
        storage.PutAsync(organizationId, storageKey, content, contentType, cancellationToken);

    public Task<string?> GetReadUrlAsync(Guid organizationId, string storageKey,
        CancellationToken cancellationToken = default) =>
        storage.GetReadUrlAsync(organizationId, storageKey, cancellationToken);
}
