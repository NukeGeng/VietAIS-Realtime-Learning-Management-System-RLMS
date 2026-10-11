namespace LearningPlatform.Classroom.Domain;

public enum HelpRequestStatus
{
    Requested,
    Acknowledged,
    Resolved
}

/// <summary>
/// Durable lifecycle for one request for teacher help. The current request is
/// also reflected on StudentActivity so realtime overview can stay small.
/// </summary>
public sealed class HelpRequest
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? LiveSessionId { get; set; }
    public Guid StudentActivityId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? QuestionId { get; set; }
    public HelpRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? HandledBy { get; set; }
}
