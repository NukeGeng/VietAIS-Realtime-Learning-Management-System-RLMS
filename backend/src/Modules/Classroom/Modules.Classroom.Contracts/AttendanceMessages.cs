namespace LearningPlatform.Classroom.Attendance;

public sealed record LiveSessionParticipantJoined(
    Guid OrganizationId,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset JoinedAt,
    string ConnectionId = "http");

public sealed record LiveSessionParticipantLeft(
    Guid OrganizationId,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset LeftAt,
    string ConnectionId = "http");

public sealed record LiveSessionParticipantReconnected(
    Guid OrganizationId,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset ReconnectedAt);

public sealed record LiveSessionEnded(
    Guid OrganizationId,
    Guid LiveSessionId,
    DateTimeOffset EndedAt);

public sealed record LiveSessionEndedForReporting(
    Guid OrganizationId,
    Guid LiveSessionId,
    DateTimeOffset EndedAt,
    Guid[] StudentIds);

public sealed record AttendanceRecordChangedForReporting(
    Guid OrganizationId,
    Guid LearningClassId,
    Guid StudentId);
