using Amazon.S3;
using Amazon.S3.Model;
using LearningPlatform.BuildingBlocks.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LearningPlatform.BuildingBlocks.Health;

public sealed class ObjectStorageHealthCheck(IAmazonS3 s3, IOptions<ObjectStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = options.Value.Bucket,
            MaxKeys = 1
        }, cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
