using LearningPlatform.Classroom.Domain;
using LearningPlatform.Academics.Domain;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Attendance;

public sealed record LiveSessionParticipantJoined(
    Guid OrganizationId,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset JoinedAt);

public sealed record LiveSessionParticipantLeft(
    Guid OrganizationId,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset LeftAt);

public sealed record LiveSessionEnded(
    Guid OrganizationId,
    Guid LiveSessionId,
    DateTimeOffset EndedAt);

public static class AttendanceMessagePublisher
{
    public static async Task PublishParticipantJoinedAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        Guid studentId,
        DateTimeOffset joinedAt)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionParticipantJoined(organizationId, liveSessionId, studentId, joinedAt));
    }

    public static async Task PublishParticipantLeftAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        Guid studentId,
        DateTimeOffset leftAt)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionParticipantLeft(organizationId, liveSessionId, studentId, leftAt));
    }

    public static async Task PublishSessionEndedAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        DateTimeOffset endedAt)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionEnded(organizationId, liveSessionId, endedAt));
    }

    private static async Task PublishAsync<T>(IMessageBus messageBus, ILogger logger, T message)
    {
        try
        {
            await messageBus.PublishAsync(message);
        }
        catch (Exception exception)
        {
            // Attendance is deliberately outside the join/leave critical path. A
            // transient queue/database issue must not take down the live room.
            logger.LogWarning(exception,
                "Unable to enqueue attendance message {MessageType} for live session.",
                typeof(T).Name);
        }
    }
}

public static class AttendanceParticipantJoinedHandler
{
    public static async Task Handle(
        LiveSessionParticipantJoined message,
        IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId)
        {
            return;
        }

        var enrollment = await session.Query<Enrollment>()
            .FirstOrDefaultAsync(value => value.LearningClassId == liveSession.LearningClassId &&
                value.StudentId == message.StudentId &&
                value.Status == EnrollmentStatus.Active, cancellationToken);
        if (enrollment is null)
        {
            return;
        }

        var id = ClassroomConstants.AttendanceRecordId(message.LiveSessionId, message.StudentId);
        var record = await session.LoadAsync<AttendanceRecord>(id, cancellationToken);
        if (record is null)
        {
            record = new AttendanceRecord
            {
                Id = id,
                OrganizationId = message.OrganizationId,
                LiveSessionId = message.LiveSessionId,
                StudentId = message.StudentId,
                FirstJoinedAt = message.JoinedAt,
                CurrentJoinedAt = message.JoinedAt,
                TotalDuration = TimeSpan.Zero,
                Status = await AttendanceHandlerRules.IsLateAsync(session, liveSession, message.JoinedAt,
                    cancellationToken)
                    ? AttendanceStatus.Late
                    : AttendanceStatus.Present,
                UpdatedAt = message.JoinedAt
            };
            session.Insert(record);
        }
        else if (record.AdjustedBy is null)
        {
            if (record.FirstJoinedAt is null || message.JoinedAt < record.FirstJoinedAt)
            {
                record.FirstJoinedAt = message.JoinedAt;
            }

            // Repeated join events are idempotent. A new open segment is only
            // started after a completed leave segment.
            if (record.CurrentJoinedAt is null)
            {
                record.CurrentJoinedAt = message.JoinedAt;
            }

            if (record.Status == AttendanceStatus.Absent)
            {
                record.Status = await AttendanceHandlerRules.IsLateAsync(session, liveSession,
                    record.FirstJoinedAt.Value, cancellationToken)
                    ? AttendanceStatus.Late
                    : AttendanceStatus.Present;
            }

            record.UpdatedAt = message.JoinedAt;
            session.Update(record);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

}

public static class AttendanceParticipantLeftHandler
{
    public static async Task Handle(
        LiveSessionParticipantLeft message,
        IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId)
        {
            return;
        }

        var enrollment = await session.Query<Enrollment>()
            .FirstOrDefaultAsync(value => value.LearningClassId == liveSession.LearningClassId &&
                value.StudentId == message.StudentId, cancellationToken);
        if (enrollment is null)
        {
            return;
        }

        var id = ClassroomConstants.AttendanceRecordId(message.LiveSessionId, message.StudentId);
        var record = await session.LoadAsync<AttendanceRecord>(id, cancellationToken);
        if (record is null)
        {
            // The join message is queued before the corresponding leave on the
            // sequential attendance queue. If an old leave is replayed first,
            // the later join creates the record and session finalization closes it.
            return;
        }
        else if (record.AdjustedBy is null)
        {
            if (record.CurrentJoinedAt is { } joinedAt && message.LeftAt > joinedAt)
            {
                record.TotalDuration += message.LeftAt - joinedAt;
                record.CurrentJoinedAt = null;
            }

            if (record.LastLeftAt is null || message.LeftAt > record.LastLeftAt)
            {
                record.LastLeftAt = message.LeftAt;
            }

            record.UpdatedAt = message.LeftAt;
            session.Update(record);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

}

public static class AttendanceSessionEndedHandler
{
    public static async Task Handle(
        LiveSessionEnded message,
        IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId)
        {
            return;
        }

        var enrollments = await session.Query<Enrollment>()
            .Where(value => value.LearningClassId == liveSession.LearningClassId &&
                value.Status == EnrollmentStatus.Active)
            .Select(value => value.StudentId)
            .ToListAsync(cancellationToken);
        foreach (var studentId in enrollments)
        {
            var id = ClassroomConstants.AttendanceRecordId(message.LiveSessionId, studentId);
            var record = await session.LoadAsync<AttendanceRecord>(id, cancellationToken);
            if (record is null)
            {
                session.Insert(new AttendanceRecord
                {
                    Id = id,
                    OrganizationId = message.OrganizationId,
                    LiveSessionId = message.LiveSessionId,
                    StudentId = studentId,
                    TotalDuration = TimeSpan.Zero,
                    Status = AttendanceStatus.Absent,
                    UpdatedAt = message.EndedAt
                });
                continue;
            }

            if (record.AdjustedBy is null && record.CurrentJoinedAt is { } joinedAt &&
                message.EndedAt > joinedAt)
            {
                record.TotalDuration += message.EndedAt - joinedAt;
                record.CurrentJoinedAt = null;
                record.LastLeftAt = message.EndedAt;
                record.UpdatedAt = message.EndedAt;
                session.Update(record);
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

}

public static class AttendanceHandlerRules
{
    public static async Task<bool> IsLateAsync(
        IDocumentSession session,
        LiveSession liveSession,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken)
    {
        if (liveSession.ClassScheduleId is not { } scheduleId)
        {
            return false;
        }

        var schedule = await session.LoadAsync<ClassSchedule>(scheduleId, cancellationToken);
        return schedule is not null && joinedAt > schedule.StartsAt;
    }
}
