using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Identity.Domain;

/// <summary>
/// Cross-module request for an in-app notification. The communication module
/// owns delivery and persistence; producers only enqueue this best-effort event
/// after their business transaction commits.
/// </summary>
public sealed record NotificationRequested(Guid EventId, Guid OrganizationId, Guid UserId,
    string Type, string Title, string Body, Guid? SourceId, DateTimeOffset OccurredAt);

/// <summary>
/// Cross-module request for a class-wide in-app notification. Communication owns
/// the enrollment fan-out so the producer does not load every student or write
/// notification documents on its critical path.
/// </summary>
public sealed record ClassNotificationRequested(Guid EventId, Guid OrganizationId,
    Guid LearningClassId, string Type, string Title, string Body, Guid? SourceId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Deterministic identifiers for cross-module notification transitions. The
/// producer can use this without taking a dependency on Communication's
/// persistence module; retries then address the same business notification.
/// </summary>
public static class NotificationEventIds
{
    public static Guid Transition(Guid sourceId, string type, DateTimeOffset occurredAt)
    {
        var value = $"notification-transition:{sourceId:D}:{type.Trim().ToLowerInvariant()}:{occurredAt.UtcTicks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
