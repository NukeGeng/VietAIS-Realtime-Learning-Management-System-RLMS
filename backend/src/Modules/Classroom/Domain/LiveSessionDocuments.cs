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

public enum AttendanceStatus
{
    Present,
    Late,
    Absent,
    Excused
}

/// <summary>
/// Durable attendance state for one student in one live session.
/// </summary>
public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LiveSessionId { get; set; }
    public Guid StudentId { get; set; }
    public DateTimeOffset? FirstJoinedAt { get; set; }
    public DateTimeOffset? LastLeftAt { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public AttendanceStatus Status { get; set; }
    public Guid? AdjustedBy { get; set; }
    public string? AdjustmentReason { get; set; }

    // This is durable so an unexpected disconnect can be finalized when the
    // session ends. It is an implementation detail, not a presence source of truth.
    public DateTimeOffset? CurrentJoinedAt { get; set; }
    // A student may have both an HTTP join and one or more SignalR connections.
    // Keep the connection identities with the open attendance segment so one
    // connection disappearing cannot close the segment for the others.
    public string[] ActiveConnectionIds { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum RaiseHandStatus
{
    Raised,
    Lowered
}

/// <summary>
/// Durable state for the current raise-hand signal of a student in a session.
/// </summary>
public sealed class RaiseHand
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LiveSessionId { get; set; }
    public Guid StudentId { get; set; }
    public RaiseHandStatus Status { get; set; }
    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset? LoweredAt { get; set; }
    public Guid? ClearedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
