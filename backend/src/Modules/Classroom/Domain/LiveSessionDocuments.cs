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
    /// <summary>
    /// Number of minutes after the scheduled/start time before a join is late.
    /// This belongs to the live-session occurrence so changing a later session
    /// does not rewrite attendance semantics for an older one.
    /// </summary>
    public int LateAfterMinutes { get; set; } = 15;
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

public sealed class AttendanceSegment
{
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public TimeSpan Duration { get; set; }
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
    // Freeze the late threshold used for this record so later schedule changes
    // cannot change its historical lateness or disagree with its stored status.
    public DateTimeOffset? LateCutoffAt { get; set; }
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
    public List<AttendanceSegment> Segments { get; set; } = [];
    public int ReconnectCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Session-scoped moderation state. It intentionally has no class-wide effect.
/// </summary>
public sealed class LiveSessionParticipantModeration
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LiveSessionId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsMuted { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }
    public DateTimeOffset? MutedAt { get; set; }
    public Guid? ChangedBy { get; set; }
    public string? Reason { get; set; }
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
