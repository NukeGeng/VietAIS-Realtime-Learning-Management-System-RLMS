using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Presence;

public sealed class LivePresenceStore(
    IConnectionMultiplexer redis,
    ILogger<LivePresenceStore> logger)
{
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(90);

    public async Task<bool> MarkJoinedAsync(Guid organizationId, Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await redis.GetDatabase().StringSetAsync(Key(organizationId, sessionId, userId),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), PresenceTtl);
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

    public async Task RemoveAsync(Guid organizationId, Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await redis.GetDatabase().KeyDeleteAsync(Key(organizationId, sessionId, userId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to clear ephemeral live presence for {UserId}.", userId);
        }
    }

    public static string Key(Guid organizationId, Guid sessionId, Guid userId) =>
        $"rlms:live-presence:{organizationId:D}:{sessionId:D}:{userId:D}";
}
