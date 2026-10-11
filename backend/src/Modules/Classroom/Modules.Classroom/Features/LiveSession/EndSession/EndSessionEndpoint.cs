using LearningPlatform.Classroom.Api;
using System.Security.Cryptography;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Classroom.Api.LiveSessionEndpoints;

namespace LearningPlatform.Classroom.Features.LiveSessions.EndSession;

internal static class EndSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/end", EndSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
    }


    private static async Task<IResult> EndSessionAsync(
        Guid sessionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
        LiveSessionSnapshotCache snapshotCache,
        CancellationToken cancellationToken)
    {
        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageAsync(context, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (liveSession.Status == LiveSessionStatus.Cancelled ||
            liveSession.Status == LiveSessionStatus.Scheduled)
        {
            return Results.Conflict(new { error = "Only a started live session can be ended." });
        }

        if (liveSession.Status != LiveSessionStatus.Ended)
        {
            liveSession.Status = LiveSessionStatus.Ended;
            liveSession.EndedAt = DateTimeOffset.UtcNow;
            liveSession.UpdatedAt = DateTimeOffset.UtcNow;

            // A schedule represents the planned occurrence that this live
            // session fulfils. Close it in the same transaction so a completed
            // occurrence cannot be rescheduled or used to create another
            // session after the room has ended.
            if (liveSession.ClassScheduleId is { } scheduleId)
            {
                var schedule = await session.LoadAsync<ClassSchedule>(scheduleId, cancellationToken);
                if (schedule is not null && schedule.LearningClassId == liveSession.LearningClassId &&
                    schedule.Status is ScheduleStatus.Scheduled or ScheduleStatus.Rescheduled)
                {
                    schedule.Status = ScheduleStatus.Completed;
                    schedule.UpdatedAt = liveSession.EndedAt.Value;
                    session.Update(schedule);
                }
            }

            session.Update(liveSession);
            await session.SaveChangesAsync(cancellationToken);
            await snapshotCache.InvalidateAsync(liveSession.OrganizationId, liveSession.Id);
            await AttendanceMessagePublisher.PublishSessionEndedAsync(messageBus, logger,
                liveSession.OrganizationId, liveSession.Id, liveSession.EndedAt.Value);
            await hub.Clients.Group(LiveSessionHub.Group(liveSession.OrganizationId, liveSession.Id))
                .SendAsync("session-ended",
                    new LiveSessionEndedSignal(liveSession.Id, liveSession.EndedAt.Value),
                    CancellationToken.None);
        }

        return Results.Ok(LiveSessionResponse.From(liveSession));
    }

}
