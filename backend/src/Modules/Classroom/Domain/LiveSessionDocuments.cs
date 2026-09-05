namespace LearningPlatform.Classroom.Domain;

public enum LiveSessionStatus
{
    Scheduled,
    Started,
    Ended,
    Cancelled
}

public sealed class LiveSession
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? ClassScheduleId { get; set; }
    public Guid TeacherId { get; set; }
    public LiveSessionStatus Status { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string MediaRoomId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
