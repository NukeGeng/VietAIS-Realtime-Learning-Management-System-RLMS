using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Presence;

public sealed class LivePresenceStore(
    IConnectionMultiplexer redis,
    ILogger<LivePresenceStore> logger)
{
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(90);
    private const string HttpConnection = "http";

    public async Task<bool> MarkJoinedAsync(Guid organizationId, Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default, string? connectionId = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var database = redis.GetDatabase();
            var key = Key(organizationId, sessionId, userId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity,
                now - PresenceTtl.TotalMilliseconds);
            await database.SortedSetAddAsync(key, NormalizeConnectionId(connectionId), now);
            await database.KeyExpireAsync(key, PresenceTtl);
            // The operation succeeded even when this connection was already present.
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to record ephemeral live presence for {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> RemoveAsync(Guid organizationId, Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default, string? connectionId = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var database = redis.GetDatabase();
            var key = Key(organizationId, sessionId, userId);
            var removed = await database.SortedSetRemoveAsync(key, NormalizeConnectionId(connectionId));
            if (!removed)
            {
                return false;
            }

            if (await database.SortedSetLengthAsync(key) == 0)
            {
                await database.KeyDeleteAsync(key);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to clear ephemeral live presence for {UserId}.", userId);
            return false;
        }
    }

    public static string Key(Guid organizationId, Guid sessionId, Guid userId) =>
        $"rlms:live-presence:{organizationId:D}:{sessionId:D}:{userId:D}";

    private static string NormalizeConnectionId(string? connectionId) =>
        string.IsNullOrWhiteSpace(connectionId) ? HttpConnection : connectionId;
}
