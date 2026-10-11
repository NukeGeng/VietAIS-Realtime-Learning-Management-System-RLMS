using LearningPlatform.BuildingBlocks.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace LearningPlatform.BuildingBlocks.Caching;

public static class CachingRegistration
{
    public static void AddCaching(this IHostApplicationBuilder builder)
    {
        var redisConnection = builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(options);
        });
        builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis",
            tags: ["peripheral"], timeout: TimeSpan.FromSeconds(5));
    }
}
