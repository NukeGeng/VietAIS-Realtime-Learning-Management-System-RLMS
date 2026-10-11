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

namespace LearningPlatform.Classroom.Features.LiveSessions.StartSession;

internal static class StartSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/start", StartSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
    }


    private static async Task<IResult> StartSessionAsync(
        Guid sessionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        LiveSessionSnapshotCache snapshotCache,
        IHubContext<LiveSessionHub> hub,
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

        if (liveSession.Status == LiveSessionStatus.Cancelled || liveSession.Status == LiveSessionStatus.Ended)
        {
            return Results.Conflict(new { error = "This live session cannot be started." });
        }

        if (liveSession.Status == LiveSessionStatus.Scheduled)
        {
            liveSession.Status = LiveSessionStatus.Started;
            liveSession.StartedAt = DateTimeOffset.UtcNow;
            liveSession.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(liveSession);
            await session.SaveChangesAsync(cancellationToken);
            await snapshotCache.InvalidateAsync(liveSession.OrganizationId, liveSession.Id);
            // A student may already be watching the scheduled room. Notify the
            // tenant-scoped SignalR group after the durable state transition so
            // that every preloaded tab can join without a reload.
            await hub.Clients.Group(LiveSessionHub.Group(liveSession.OrganizationId, liveSession.Id))
                .SendAsync("session-started", new LiveSessionStartedSignal(liveSession.Id,
                    liveSession.StartedAt.Value), CancellationToken.None);
        }

        return Results.Ok(LiveSessionResponse.From(liveSession));
    }

}
