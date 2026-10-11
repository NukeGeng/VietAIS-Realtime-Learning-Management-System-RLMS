namespace LearningPlatform.Communication.Domain;

/// <summary>
/// A tenant-owned message sent to a live classroom session.
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LiveSessionId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}
