using LearningPlatform.Classroom.Api;
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
using static LearningPlatform.Classroom.Api.AttendanceEndpoints;

namespace LearningPlatform.Classroom.Features.Attendance.AdjustAttendance;

internal static class AdjustAttendanceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/attendance/{studentId:guid}",
                AdjustAttendanceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAttendance);
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
