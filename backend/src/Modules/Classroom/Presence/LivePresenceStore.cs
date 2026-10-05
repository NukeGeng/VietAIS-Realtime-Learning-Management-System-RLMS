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
            var usersKey = UsersKey(organizationId, sessionId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity,
                now - PresenceTtl.TotalMilliseconds);
            await database.SortedSetAddAsync(key, NormalizeConnectionId(connectionId), now);
            await database.KeyExpireAsync(key, PresenceTtl);
            // HTTP join is tracked for attendance, but only a SignalR
            // connection is a media peer that can receive signaling.
            if (connectionId is not null)
            {
                await database.SortedSetAddAsync(usersKey, userId.ToString("D"), now);
                await database.KeyExpireAsync(usersKey, PresenceTtl);
            }
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
            var usersKey = UsersKey(organizationId, sessionId);
            var removed = await database.SortedSetRemoveAsync(key, NormalizeConnectionId(connectionId));
            if (!removed)
            {
                return false;
            }

            if (await database.SortedSetLengthAsync(key) == 0)
            {
                await database.KeyDeleteAsync(key);
                await database.SortedSetRemoveAsync(usersKey, userId.ToString("D"));
                if (await database.SortedSetLengthAsync(usersKey) == 0)
                {
                    await database.KeyDeleteAsync(usersKey);
                }
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

    /// <summary>
    /// Removes every ephemeral connection for one user in this session. This is
    /// used by session-scoped moderation; durable attendance remains separate.
    /// </summary>
    public async Task<bool> RemoveUserAsync(Guid organizationId, Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var database = redis.GetDatabase();
            var removed = await database.KeyDeleteAsync(Key(organizationId, sessionId, userId));
            await database.SortedSetRemoveAsync(UsersKey(organizationId, sessionId), userId.ToString("D"));
            return removed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to remove moderated live presence for {UserId}.", userId);
            return false;
        }
    }

    public static string Key(Guid organizationId, Guid sessionId, Guid userId) =>
        $"rlms:live-presence:{organizationId:D}:{sessionId:D}:{userId:D}";

    /// <summary>
    /// Returns the users with a recent presence heartbeat in one live session.
    /// The index is ephemeral and is used only for media peer discovery.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetUsersAsync(Guid organizationId, Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var database = redis.GetDatabase();
            var usersKey = UsersKey(organizationId, sessionId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await database.SortedSetRemoveRangeByScoreAsync(usersKey, double.NegativeInfinity,
                now - PresenceTtl.TotalMilliseconds);
            var members = await database.SortedSetRangeByRankAsync(usersKey, 0, -1);
            return members
                .Select(value => Guid.TryParse(value.ToString(), out var userId) ? userId : Guid.Empty)
                .Where(userId => userId != Guid.Empty)
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to list live-session participants for {SessionId}.", sessionId);
            return [];
        }
    }

    public static string UsersKey(Guid organizationId, Guid sessionId) =>
        $"rlms:live-presence-users:{organizationId:D}:{sessionId:D}";

    private static string NormalizeConnectionId(string? connectionId) =>
        string.IsNullOrWhiteSpace(connectionId) ? HttpConnection : connectionId;
}
