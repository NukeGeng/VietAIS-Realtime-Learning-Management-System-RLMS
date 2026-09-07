namespace LearningPlatform.Learning.Storage;

public interface ILearningResourceStorage
{
    Task PutAsync(Guid organizationId, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);

    Task<string?> GetReadUrlAsync(Guid organizationId, string storageKey,
        CancellationToken cancellationToken = default);
}
