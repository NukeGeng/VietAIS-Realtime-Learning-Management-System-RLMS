using Amazon.Runtime;
using Amazon.S3;
using JasperFx;
using LearningPlatform.Infrastructure.Health;
using LearningPlatform.Infrastructure.Media;
using LearningPlatform.Infrastructure.Persistence;
using LearningPlatform.Infrastructure.Storage;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using Wolverine.Marten;

namespace LearningPlatform.Infrastructure;

public static class InfrastructureRegistration
{
    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        var postgres = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        var redisConnection = builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

        builder.Services.AddMarten(options =>
        {
            options.Connection(postgres);
            options.DatabaseSchemaName = "rlms";
            options.Policies.AllDocumentsAreMultiTenanted();
            options.AutoCreateSchemaObjects = builder.Environment.IsDevelopment()
                ? AutoCreate.CreateOrUpdate : AutoCreate.None;
        }).UseLightweightSessions().IntegrateWithWolverine();

        builder.Services.AddHttpContextAccessor();
        // Override default HTTP session resolution: there is no unscoped fallback.
        builder.Services.AddScoped<IDocumentSession>(sp => OrganizationSession.Open(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()));
        builder.Services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentSession>());
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(postgres));
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(options);
        });
        builder.Services.AddOptions<ObjectStorageOptions>()
            .BindConfiguration(ObjectStorageOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<MediaOptions>().BindConfiguration("Media")
            .ValidateDataAnnotations()
            .Validate(o => Uri.TryCreate(o.ServerUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "ws" or "wss", "Media:ServerUrl must use ws or wss.")
            .ValidateOnStart();
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
        builder.Services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["core"], timeout: TimeSpan.FromSeconds(5))
            .AddCheck<RedisHealthCheck>("redis", tags: ["peripheral"], timeout: TimeSpan.FromSeconds(5))
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["peripheral"], timeout: TimeSpan.FromSeconds(5));
    }
}
