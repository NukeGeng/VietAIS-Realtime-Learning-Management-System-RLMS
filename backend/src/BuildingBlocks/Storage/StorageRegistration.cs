using LearningPlatform.BuildingBlocks.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using LearningPlatform.BuildingBlocks.Storage;

namespace LearningPlatform.BuildingBlocks.Storage;

public static class StorageRegistration
{
    public static void AddObjectStorage(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<ObjectStorageOptions>()
            .BindConfiguration(ObjectStorageOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = options.Endpoint,
                    AuthenticationRegion = options.Region,
                    ForcePathStyle = true,
                    Timeout = TimeSpan.FromSeconds(5)
                });
        });
        builder.Services.AddSingleton<IObjectStorage, S3FileStorage>();
        builder.Services.AddHealthChecks().AddCheck<ObjectStorageHealthCheck>("object-storage",
            tags: ["peripheral"], timeout: TimeSpan.FromSeconds(5));
    }
}
