using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Presence;

public sealed record LivePresentationLease(
    Guid UserId,
    bool IsTeacher,
    string ConnectionId,
    DateTimeOffset StartedAt);

public sealed record LivePresentationAcquireResult(
    bool Acquired,
    LivePresentationLease? Current,
    LivePresentationLease? Replaced);

/// <summary>
/// Keeps the single presenter lease for one live session in Redis. The lease is
/// ephemeral; durable attendance and class state remain in their own stores.
/// </summary>
public sealed class LivePresentationStore(
    IConnectionMultiplexer redis,
    ILogger<LivePresentationStore> logger)
{
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromHours(6);

    private const string AcquireScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
          redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[4])
          return { '1', '' }
        end
        local owner = string.match(current, '^([^|]+)')
        if owner == ARGV[2] then
          redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[4])
          return { '1', '' }
        end
        if ARGV[3] == '1' then
          redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[4])
          return { '1', current }
        end
        return { '0', current }
        """;

    private const string ReleaseScript = """
        local current = redis.call('GET', KEYS[1])
        if current and string.sub(current, 1, string.len(ARGV[1])) == ARGV[1] then
          redis.call('DEL', KEYS[1])
          return '1'
        end
        return '0'
        """;

    private const string RefreshScript = """
        local current = redis.call('GET', KEYS[1])
        if current and string.sub(current, 1, string.len(ARGV[1])) == ARGV[1] then
          redis.call('EXPIRE', KEYS[1], ARGV[2])
          return '1'
        end
        return '0'
        """;

    public async Task<LivePresentationAcquireResult> TryAcquireAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        bool isTeacher,
        string connectionId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lease = Encode(userId, isTeacher, connectionId, DateTimeOffset.UtcNow);
            var evaluated = await redis.GetDatabase().ScriptEvaluateAsync(
                AcquireScript,
                [new RedisKey(Key(organizationId, sessionId))],
                [lease, userId.ToString("D"), force ? "1" : "0", (long)LeaseTtl.TotalSeconds]);
            var result = evaluated.IsNull
                ? Array.Empty<RedisResult>()
                : ((RedisResult[]?)evaluated ?? Array.Empty<RedisResult>());
            var acquired = result.Length > 0 && result[0].ToString() == "1";
            var previous = result.Length > 1 ? Decode(result[1].ToString()) : null;
            return acquired
                ? new LivePresentationAcquireResult(true, Decode(lease), previous)
                : new LivePresentationAcquireResult(false, previous, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to reserve live presentation for {SessionId}.", sessionId);
            return new LivePresentationAcquireResult(false, null, null);
        }
    }

    public async Task<bool> ReleaseAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = Prefix(userId, connectionId);
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                ReleaseScript,
                [new RedisKey(Key(organizationId, sessionId))],
                [prefix]);
            return result.ToString() == "1";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to release live presentation for {SessionId}.", sessionId);
            return false;
        }
    }

    public async Task<bool> RefreshAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                RefreshScript,
                [new RedisKey(Key(organizationId, sessionId))],
                [Prefix(userId, connectionId), (long)LeaseTtl.TotalSeconds]);
            return result.ToString() == "1";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to refresh live presentation for {SessionId}.", sessionId);
            return false;
        }
    }

    public async Task<LivePresentationLease?> GetAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await redis.GetDatabase().StringGetAsync(Key(organizationId, sessionId));
            return value.HasValue ? Decode(value.ToString()) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to read live presentation for {SessionId}.", sessionId);
            return null;
        }
    }

    public static string Key(Guid organizationId, Guid sessionId) =>
        $"rlms:live-presentation:{organizationId:D}:{sessionId:D}";

    private static string Prefix(Guid userId, string connectionId) =>
        $"{userId:D}|{connectionId}|";

    private static string Encode(Guid userId, bool isTeacher, string connectionId, DateTimeOffset startedAt) =>
        $"{Prefix(userId, connectionId)}{(isTeacher ? '1' : '0')}|{startedAt:O}";

    private static LivePresentationLease? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('|', 4);
        if (parts.Length != 4 || !Guid.TryParse(parts[0], out var userId) ||
            !DateTimeOffset.TryParse(parts[3], out var startedAt)) return null;
        return new LivePresentationLease(userId, parts[2] == "1", parts[1], startedAt);
    }
}
