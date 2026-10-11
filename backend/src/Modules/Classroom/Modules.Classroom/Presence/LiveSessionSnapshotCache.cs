using System.Collections.Concurrent;
using System.Text.Json;
using LearningPlatform.Classroom.Domain;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LearningPlatform.Classroom.Presence;

/// <summary>
/// Reuses the shared live-session metadata during mass join. Redis is only a
/// cache; a miss always falls back to the durable Marten document.
/// </summary>
public sealed class LiveSessionSnapshotCache(
    IConnectionMultiplexer redis,
    ILogger<LiveSessionSnapshotCache> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Lazy<Task<LiveSession?>>> inFlight = new();

    public async Task<LiveSession?> GetAsync(Guid organizationId, Guid sessionId,
        Func<CancellationToken, Task<LiveSession?>> loader,
        CancellationToken cancellationToken = default)
    {
        var key = Key(organizationId, sessionId);
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key);
            if (cached.HasValue)
            {
                var snapshot = JsonSerializer.Deserialize<LiveSession>(cached.ToString(), JsonOptions);
                if (snapshot is not null && snapshot.OrganizationId == organizationId &&
                    snapshot.Id == sessionId)
                {
                    return snapshot;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to read cached live session {SessionId}.", sessionId);
        }

        var lazy = inFlight.GetOrAdd(key, _ => new Lazy<Task<LiveSession?>>(
            () => LoadAndCacheAsync(key, loader), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompleted)
            {
                inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<LiveSession?>>>(key, lazy));
            }
        }
    }

    public async Task InvalidateAsync(Guid organizationId, Guid sessionId)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(Key(organizationId, sessionId));
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to invalidate cached live session {SessionId}.", sessionId);
        }
    }

    public static string Key(Guid organizationId, Guid sessionId) =>
        $"rlms:live-session-snapshot:{organizationId:D}:{sessionId:D}";

    private async Task<LiveSession?> LoadAndCacheAsync(string key,
        Func<CancellationToken, Task<LiveSession?>> loader)
    {
        var liveSession = await loader(CancellationToken.None);
        if (liveSession is null)
        {
            return null;
        }

        try
        {
            var serialized = JsonSerializer.Serialize(liveSession, JsonOptions);
            await redis.GetDatabase().StringSetAsync(key, serialized, Ttl);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to cache live session {SessionId}.", liveSession.Id);
        }

        return liveSession;
    }
}
