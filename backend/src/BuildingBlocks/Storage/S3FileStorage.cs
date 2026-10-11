using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace LearningPlatform.BuildingBlocks.Storage;

public sealed class S3FileStorage(IAmazonS3 client, IOptions<ObjectStorageOptions> options)
    : LearningPlatform.BuildingBlocks.Storage.IObjectStorage
{
    private readonly ObjectStorageOptions storageOptions = options.Value;

    public Task PutAsync(Guid organizationId, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(organizationId, storageKey);

        return client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = storageOptions.Bucket,
            Key = storageKey,
            InputStream = content,
            ContentType = contentType
        }, cancellationToken);
    }

    public Task<string?> GetReadUrlAsync(Guid organizationId, string storageKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(organizationId, storageKey);
        cancellationToken.ThrowIfCancellationRequested();
        var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = storageOptions.Bucket,
            Key = storageKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(10)
        });
        return Task.FromResult<string?>(url);
    }

    private static void ValidateKey(Guid organizationId, string storageKey)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(storageKey) ||
            !storageKey.StartsWith($"{organizationId:D}/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Object storage keys must be tenant-prefixed.");
        }
    }
}
