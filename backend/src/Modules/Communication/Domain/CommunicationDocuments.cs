using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Communication.Domain;

public enum AnnouncementStatus
{
    Draft,
    Published,
    Archived
}

public sealed class Announcement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public AnnouncementStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public Guid? SourceId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class CommunicationConstants
{
    public const int MaxAnnouncementTitleLength = 200;
    public const int MaxAnnouncementContentLength = 20_000;
    public const int MaxNotificationTypeLength = 100;
    public const int MaxNotificationTitleLength = 200;
    public const int MaxNotificationBodyLength = 4_000;

    public static Guid AnnouncementNotificationId(Guid userId, Guid announcementId) =>
        StableId($"announcement-notification:{userId:D}:{announcementId:D}");

    public static Guid NotificationId(Guid userId, string type, Guid? sourceId, Guid eventId)
    {
        // Class-wide notifications use the same event for every recipient. The
        // recipient must therefore always participate in the idempotency key;
        // otherwise the first student's document would suppress the fan-out.
        var source = sourceId?.ToString("D") ?? "none";
        return StableId($"notification:{userId:D}:{type}:{source}:{eventId:D}");
    }

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}

public static class CommunicationRules
{
    public static string RequireAnnouncementTitle(string? value)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length == 0 || normalized.Length > CommunicationConstants.MaxAnnouncementTitleLength)
        {
            throw new CommunicationValidationException("Announcement title is required and must be at most 200 characters.");
        }

        return normalized;
    }

    public static string RequireAnnouncementContent(string? value)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length == 0 || normalized.Length > CommunicationConstants.MaxAnnouncementContentLength)
        {
            throw new CommunicationValidationException("Announcement content is required and must be at most 20000 characters.");
        }

        return normalized;
    }
}

public sealed class CommunicationValidationException(string message) : Exception(message);
