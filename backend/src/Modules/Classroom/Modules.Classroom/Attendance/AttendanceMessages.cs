using LearningPlatform.Classroom.Domain;
using LearningPlatform.Academics.Domain;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Attendance;

public static class AttendanceMessagePublisher
{
    public static async Task PublishParticipantJoinedAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        Guid studentId,
        DateTimeOffset joinedAt,
        string? connectionId = null)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionParticipantJoined(organizationId, liveSessionId, studentId, joinedAt,
                AttendanceConnection.Normalize(connectionId)));
    }

    public static async Task PublishParticipantLeftAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        Guid studentId,
        DateTimeOffset leftAt,
        string? connectionId = null)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionParticipantLeft(organizationId, liveSessionId, studentId, leftAt,
                AttendanceConnection.Normalize(connectionId)));
    }

    public static async Task PublishParticipantReconnectedAsync(
        IMessageBus messageBus,
        ILogger logger,
        Guid organizationId,
        Guid liveSessionId,
        Guid studentId,
        DateTimeOffset reconnectedAt)
    {
        await PublishAsync(messageBus, logger,
            new LiveSessionParticipantReconnected(organizationId, liveSessionId, studentId, reconnectedAt));
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
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId ||
            liveSession.Status is not (LiveSessionStatus.Started or LiveSessionStatus.Ended))
        {
            return;
        }

        // A join accepted before the room ended can reach this sequential
        // handler after the end message. Preserve that historical join and
        // close it at the durable end time; a join created after the end is
        // ignored so it cannot resurrect attendance.
        if (liveSession.Status == LiveSessionStatus.Ended &&
            (liveSession.EndedAt is null || message.JoinedAt >= liveSession.EndedAt.Value))
        {
            return;
        }

        var enrollment = await session.Query<Enrollment>()
            .FirstOrDefaultAsync(value => value.OrganizationId == message.OrganizationId &&
                value.LearningClassId == liveSession.LearningClassId &&
                value.StudentId == message.StudentId &&
                value.Status == EnrollmentStatus.Active, cancellationToken);
        if (enrollment is null)
        {
            return;
        }

        var id = ClassroomConstants.AttendanceRecordId(message.LiveSessionId, message.StudentId);
        var record = await session.LoadAsync<AttendanceRecord>(id, cancellationToken);
        var connectionId = AttendanceConnection.Normalize(message.ConnectionId);
        var inserted = false;
        if (record is null)
        {
            var lateCutoff = await AttendanceHandlerRules.ResolveLateCutoffAsync(session, liveSession,
                cancellationToken);
            record = new AttendanceRecord
            {
                Id = id,
                OrganizationId = message.OrganizationId,
                LiveSessionId = message.LiveSessionId,
                StudentId = message.StudentId,
                FirstJoinedAt = message.JoinedAt,
                LateCutoffAt = lateCutoff,
                CurrentJoinedAt = message.JoinedAt,
                TotalDuration = TimeSpan.Zero,
                Status = lateCutoff is not null && message.JoinedAt > lateCutoff
                    ? AttendanceStatus.Late
                    : AttendanceStatus.Present,
                ActiveConnectionIds = [connectionId],
                Segments = [new AttendanceSegment { JoinedAt = message.JoinedAt }],
                UpdatedAt = message.JoinedAt
            };
            session.Insert(record);
            inserted = true;
        }
        else if (record.AdjustedBy is null)
        {
            AttendanceConnection.EnsureLegacyConnection(record);
            if (record.FirstJoinedAt is null || message.JoinedAt < record.FirstJoinedAt)
            {
                record.FirstJoinedAt = message.JoinedAt;
                // A delayed earlier join can correct an automatic Late status.
                // Use the frozen cutoff; legacy/manual records keep their history.
                if (record.LateCutoffAt is { } cutoff &&
                    record.Status is AttendanceStatus.Present or AttendanceStatus.Late)
                {
                    record.Status = message.JoinedAt > cutoff
                        ? AttendanceStatus.Late
                        : AttendanceStatus.Present;
                }
            }

            // Repeated join events are idempotent. A new open segment is only
            // started after a completed leave segment, while additional
            // connections share the existing segment.
            if (!record.ActiveConnectionIds.Contains(connectionId, StringComparer.Ordinal))
            {
                if (record.CurrentJoinedAt is null)
                {
                    AttendanceHistory.StartSegment(record, message.JoinedAt);
                }

                record.ActiveConnectionIds = [.. record.ActiveConnectionIds, connectionId];
            }

            if (record.Status == AttendanceStatus.Absent)
            {
                record.LateCutoffAt ??= await AttendanceHandlerRules.ResolveLateCutoffAsync(session,
                    liveSession, cancellationToken);
                record.Status = record.LateCutoffAt is not null && record.FirstJoinedAt > record.LateCutoffAt
                    ? AttendanceStatus.Late
                    : AttendanceStatus.Present;
            }

            record.UpdatedAt = message.JoinedAt;
            session.Update(record);
        }

        if (liveSession.Status == LiveSessionStatus.Ended && liveSession.EndedAt is { } endedAt &&
            record.AdjustedBy is null)
        {
            if (record.CurrentJoinedAt is { } joinedAt && endedAt > joinedAt)
            {
                AttendanceHistory.CloseSegment(record, endedAt);
                record.LastLeftAt = record.LastLeftAt is { } lastLeftAt && lastLeftAt > endedAt
                    ? lastLeftAt
                    : endedAt;
            }

            record.ActiveConnectionIds = [];
            record.UpdatedAt = endedAt;
            if (!inserted)
            {
                session.Update(record);
            }
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
            .FirstOrDefaultAsync(value => value.OrganizationId == message.OrganizationId &&
                value.LearningClassId == liveSession.LearningClassId &&
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
            AttendanceConnection.EnsureLegacyConnection(record);
            if (!record.ActiveConnectionIds.Contains(AttendanceConnection.Normalize(message.ConnectionId),
                    StringComparer.Ordinal))
            {
                return;
            }

            record.ActiveConnectionIds = record.ActiveConnectionIds
                .Where(value => !string.Equals(value, AttendanceConnection.Normalize(message.ConnectionId),
                    StringComparison.Ordinal))
                .ToArray();
            if (record.ActiveConnectionIds.Length == 0 && record.CurrentJoinedAt is { } joinedAt &&
                message.LeftAt > joinedAt)
            {
                AttendanceHistory.CloseSegment(record, message.LeftAt);
                if (record.LastLeftAt is null || message.LeftAt > record.LastLeftAt)
                {
                    record.LastLeftAt = message.LeftAt;
                }
            }

            record.UpdatedAt = message.LeftAt;
            session.Update(record);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

}

public static class AttendanceParticipantReconnectedHandler
{
    public static async Task Handle(
        LiveSessionParticipantReconnected message,
        IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId ||
            liveSession.Status != LiveSessionStatus.Started)
        {
            return;
        }

        var enrollment = await session.Query<Enrollment>()
            .FirstOrDefaultAsync(value => value.OrganizationId == message.OrganizationId &&
                value.LearningClassId == liveSession.LearningClassId &&
                value.StudentId == message.StudentId && value.Status == EnrollmentStatus.Active,
                cancellationToken);
        if (enrollment is null)
        {
            return;
        }

        var record = await session.LoadAsync<AttendanceRecord>(
            ClassroomConstants.AttendanceRecordId(message.LiveSessionId, message.StudentId), cancellationToken);
        if (record is null || record.AdjustedBy is not null)
        {
            return;
        }

        AttendanceHistory.EnsureSegments(record);
        if (record.CurrentJoinedAt is { } joinedAt && message.ReconnectedAt > joinedAt)
        {
            AttendanceHistory.CloseSegment(record, message.ReconnectedAt);
            record.LastLeftAt = message.ReconnectedAt;
        }

        if (record.CurrentJoinedAt is null)
        {
            AttendanceHistory.StartSegment(record, message.ReconnectedAt);
        }

        record.UpdatedAt = message.ReconnectedAt;
        session.Update(record);
        await session.SaveChangesAsync(cancellationToken);
    }
}

public static class AttendanceSessionEndedHandler
{
    public static async Task Handle(
        LiveSessionEnded message,
        IDocumentStore store,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        if (liveSession is null || liveSession.OrganizationId != message.OrganizationId)
        {
            return;
        }

        var enrollments = await session.Query<Enrollment>()
            .Where(value => value.OrganizationId == message.OrganizationId &&
                value.LearningClassId == liveSession.LearningClassId &&
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

            if (record.AdjustedBy is null)
            {
                AttendanceConnection.EnsureLegacyConnection(record);
                var changed = record.ActiveConnectionIds.Length > 0;
                if (record.CurrentJoinedAt is { } joinedAt && message.EndedAt > joinedAt)
                {
                    AttendanceHistory.CloseSegment(record, message.EndedAt);
                    record.LastLeftAt = message.EndedAt;
                    changed = true;
                }

                record.ActiveConnectionIds = [];
                if (changed)
                {
                    record.UpdatedAt = message.EndedAt;
                    session.Update(record);
                }
            }
        }

        await session.SaveChangesAsync(cancellationToken);

        // Reporting must observe the finalized attendance documents. Publish
        // its refresh only after this transaction commits; the two queues are
        // independent and publishing both messages at the HTTP boundary could
        // otherwise let reporting race ahead of attendance finalization.
        await messageBus.PublishAsync(new LiveSessionEndedForReporting(
            message.OrganizationId, message.LiveSessionId, message.EndedAt, enrollments.ToArray()));
    }

}

internal static class AttendanceConnection
{
    private const string HttpConnection = "http";

    public static string Normalize(string? connectionId) =>
        string.IsNullOrWhiteSpace(connectionId) ? HttpConnection : connectionId;

    public static void EnsureLegacyConnection(AttendanceRecord record)
    {
        // Records written before connection-aware attendance only had an open
        // segment marker. Treat that segment as the HTTP connection so it can
        // be closed safely by the existing leave endpoint.
        record.ActiveConnectionIds ??= [];
        if (record.CurrentJoinedAt is not null && record.ActiveConnectionIds.Length == 0)
        {
            record.ActiveConnectionIds = [HttpConnection];
        }
    }
}

public static class AttendanceHandlerRules
{
    public static async Task<DateTimeOffset?> ResolveLateCutoffAsync(
        IDocumentSession session, LiveSession liveSession, CancellationToken cancellationToken)
    {
        var schedule = liveSession.ClassScheduleId is { } scheduleId
            ? await session.LoadAsync<ClassSchedule>(scheduleId, cancellationToken)
            : null;
        return LateCutoff(liveSession, schedule);
    }

    public static DateTimeOffset? LateCutoff(LiveSession liveSession, ClassSchedule? schedule)
    {
        var startsAt = schedule?.StartsAt ?? liveSession.StartedAt;
        // A room opened after a planned occurrence uses its actual runtime start.
        if (schedule is not null && liveSession.StartedAt is { } runtimeStart &&
            runtimeStart > schedule.EndsAt)
            startsAt = runtimeStart;
        return startsAt?.AddMinutes(liveSession.LateAfterMinutes);
    }

}

internal static class AttendanceHistory
{
    public static void EnsureSegments(AttendanceRecord record)
    {
        record.Segments ??= [];
        if (record.Segments.Count == 0 && record.CurrentJoinedAt is { } joinedAt)
        {
            record.Segments.Add(new AttendanceSegment { JoinedAt = joinedAt });
        }
    }

    public static void StartSegment(AttendanceRecord record, DateTimeOffset joinedAt)
    {
        EnsureSegments(record);
        if (record.CurrentJoinedAt is not null)
        {
            return;
        }

        if (record.Segments.Count > 0)
        {
            record.ReconnectCount++;
        }

        record.CurrentJoinedAt = joinedAt;
        record.Segments.Add(new AttendanceSegment { JoinedAt = joinedAt });
    }

    public static void CloseSegment(AttendanceRecord record, DateTimeOffset leftAt)
    {
        EnsureSegments(record);
        if (record.CurrentJoinedAt is not { } joinedAt || leftAt <= joinedAt)
        {
            return;
        }

        var duration = leftAt - joinedAt;
        var segment = record.Segments.LastOrDefault(value => value.LeftAt is null);
        if (segment is null)
        {
            segment = new AttendanceSegment { JoinedAt = joinedAt };
            record.Segments.Add(segment);
        }

        segment.LeftAt = leftAt;
        segment.Duration = duration;
        record.TotalDuration += duration;
        record.CurrentJoinedAt = null;
    }
}
