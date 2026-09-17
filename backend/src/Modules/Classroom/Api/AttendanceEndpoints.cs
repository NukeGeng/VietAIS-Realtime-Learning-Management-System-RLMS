using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Api;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/attendance", ListAttendanceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAttendance);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/attendance/{studentId:guid}",
                AdjustAttendanceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAttendance);
    }

    private static async Task<IResult> ListAttendanceAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.OrganizationId != actor!.OrganizationId)
        {
            return Results.Forbid();
        }

        if (!await LiveSessionAuthorization.CanManageAttendanceAsync(actor, liveSession, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await session.Query<AttendanceRecord>()
            .Where(record => record.OrganizationId == actor.OrganizationId &&
                record.LiveSessionId == sessionId)
            .OrderBy(record => record.StudentId)
            .ToListAsync(cancellationToken);
        var totalStudents = await session.Query<Enrollment>()
            .CountAsync(enrollment => enrollment.OrganizationId == actor.OrganizationId &&
                enrollment.LearningClassId == liveSession.LearningClassId &&
                enrollment.Status == EnrollmentStatus.Active, cancellationToken);
        return Results.Ok(AttendanceSummaryResponse.From(records, totalStudents));
    }

    private static async Task<IResult> AdjustAttendanceAsync(
        Guid sessionId,
        Guid studentId,
        AttendanceAdjustmentRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await LiveSessionAuthorization.CanManageAttendanceAsync(actor!, liveSession, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, liveSession.LearningClassId,
                studentId, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!Enum.TryParse<AttendanceStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Results.BadRequest(new { error = "Status must be Present, Late, Absent, or Excused." });
        }

        try
        {
            var reason = ClassroomInteractionRules.RequireAdjustmentReason(request.Reason);
            var now = DateTimeOffset.UtcNow;
            var id = ClassroomConstants.AttendanceRecordId(sessionId, studentId);
            var record = await session.LoadAsync<AttendanceRecord>(id, cancellationToken);
            var isNew = record is null;
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    Id = id,
                    OrganizationId = actor.OrganizationId,
                    LiveSessionId = sessionId,
                    StudentId = studentId,
                    TotalDuration = TimeSpan.Zero
                };
                session.Insert(record);
            }
            else if (record.CurrentJoinedAt is { } joinedAt && now > joinedAt)
            {
                AttendanceHistory.CloseSegment(record, now);
                record.ActiveConnectionIds = [];
                record.LastLeftAt = now;
            }

            record.ActiveConnectionIds = [];

            record.Status = status;
            record.AdjustedBy = actor.UserId;
            record.AdjustmentReason = reason;
            record.UpdatedAt = now;
            if (!isNew)
            {
                session.Update(record);
            }
            await session.SaveChangesAsync(cancellationToken);

            try
            {
                await messageBus.PublishAsync(new AttendanceRecordChangedForReporting(
                    actor.OrganizationId, liveSession.LearningClassId, studentId));
            }
            catch (Exception exception)
            {
                // Reporting is asynchronous and must not make a durable
                // attendance correction fail.
                logger.LogWarning(exception,
                    "Unable to enqueue attendance reporting refresh for {SessionId}/{StudentId}.",
                    sessionId, studentId);
            }

            var response = AttendanceRecordResponse.From(record);
            try
            {
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("attendance-adjusted", response, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // A SignalR outage must not roll back a durable attendance correction.
                logger.LogWarning(exception, "Unable to broadcast attendance adjustment for {SessionId}.",
                    sessionId);
            }

            return Results.Ok(response);
        }
        catch (ClassroomInteractionValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

public sealed record AttendanceAdjustmentRequest(string? Status, string? Reason);

public sealed record AttendanceRecordResponse(
    Guid Id,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset? FirstJoinedAt,
    DateTimeOffset? LastLeftAt,
    TimeSpan TotalDuration,
    AttendanceStatus Status,
    Guid? AdjustedBy,
    string? AdjustmentReason,
    int ReconnectCount,
    IReadOnlyList<AttendanceSegmentResponse> Segments)
{
    public static AttendanceRecordResponse From(AttendanceRecord record) => new(record.Id,
        record.LiveSessionId, record.StudentId, record.FirstJoinedAt, record.LastLeftAt,
        record.TotalDuration, record.Status, record.AdjustedBy, record.AdjustmentReason,
        record.ReconnectCount,
        (record.Segments ?? []).Select(AttendanceSegmentResponse.From).ToArray());
}

public sealed record AttendanceSegmentResponse(
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt,
    TimeSpan Duration)
{
    public static AttendanceSegmentResponse From(AttendanceSegment segment) =>
        new(segment.JoinedAt, segment.LeftAt, segment.Duration);
}

public sealed record AttendanceSummaryResponse(
    int TotalStudents,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    int ExcusedCount,
    double AttendancePercentage,
    IReadOnlyList<AttendanceRecordResponse> Records)
{
    public static AttendanceSummaryResponse From(IReadOnlyCollection<AttendanceRecord> records,
        int totalStudents)
    {
        var present = records.Count(record => record.Status == AttendanceStatus.Present);
        var late = records.Count(record => record.Status == AttendanceStatus.Late);
        var absent = records.Count(record => record.Status == AttendanceStatus.Absent);
        var excused = records.Count(record => record.Status == AttendanceStatus.Excused);
        var attended = present + late + excused;
        var percentage = totalStudents == 0 ? 0 : Math.Round(attended * 100d / totalStudents, 2);
        return new AttendanceSummaryResponse(totalStudents, present, late, absent, excused, percentage,
            records.Select(AttendanceRecordResponse.From).ToArray());
    }
}
