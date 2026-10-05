using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Api;

public static class LiveSessionModerationEndpoints
{
    public static void MapLiveSessionModerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/moderation", ListAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/moderation/{studentId:guid}/{action}", UpdateAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
    }

    private static async Task<IResult> ListAsync(
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

        if (!await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await session.Query<LiveSessionParticipantModeration>()
            .Where(value => value.OrganizationId == actor!.OrganizationId && value.LiveSessionId == sessionId)
            .OrderBy(value => value.StudentId)
            .ToListAsync(cancellationToken);
        return Results.Ok(records.Select(LiveSessionModerationResponse.From).ToArray());
    }

    private static async Task<IResult> UpdateAsync(
        Guid sessionId,
        Guid studentId,
        string action,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        LivePresentationStore presentation,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
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

        if (liveSession.Status != LiveSessionStatus.Started ||
            !await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (studentId == liveSession.TeacherId ||
            !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, liveSession.LearningClassId,
                studentId, cancellationToken))
        {
            return Results.NotFound();
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        if (normalizedAction is not ("mute" or "unmute" or "kick" or "block" or "unblock"))
        {
            return Results.BadRequest(new { error = "Action must be mute, unmute, kick, block, or unblock." });
        }

        var now = DateTimeOffset.UtcNow;
        var id = ClassroomConstants.ModerationId(sessionId, studentId);
        var record = await session.LoadAsync<LiveSessionParticipantModeration>(id, cancellationToken);
        var isKick = normalizedAction == "kick";
        if (!isKick)
        {
            record ??= new LiveSessionParticipantModeration
            {
                Id = id,
                OrganizationId = actor.OrganizationId,
                LiveSessionId = sessionId,
                StudentId = studentId
            };

            switch (normalizedAction)
            {
                case "mute":
                    record.IsMuted = true;
                    record.MutedAt = now;
                    break;
                case "unmute":
                    record.IsMuted = false;
                    record.MutedAt = null;
                    break;
                case "block":
                    record.IsBlocked = true;
                    record.BlockedAt = now;
                    break;
                case "unblock":
                    record.IsBlocked = false;
                    record.BlockedAt = null;
                    break;
            }

            record.ChangedBy = actor.UserId;
            record.UpdatedAt = now;
            session.Store(record);
            await session.SaveChangesAsync(cancellationToken);
        }

        try
        {
            if (normalizedAction is "kick" or "block")
            {
                await presence.RemoveUserAsync(actor.OrganizationId, sessionId, studentId, cancellationToken);
                var currentPresentation = await presentation.GetAsync(actor.OrganizationId, sessionId,
                    cancellationToken);
                var stoppedPresentation = currentPresentation?.UserId == studentId &&
                    await presentation.ReleaseAsync(actor.OrganizationId, sessionId, studentId,
                        currentPresentation.ConnectionId, cancellationToken);
                if (stoppedPresentation)
                {
                    await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                        .SendAsync("presentation-stopped",
                            new PresentationStoppedSignal(studentId, now), CancellationToken.None);
                }
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("participant-kicked", new ModerationSignal(studentId, normalizedAction, now),
                        CancellationToken.None);
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("participant-left", new PresenceDelta(studentId, now), CancellationToken.None);
            }
            else
            {
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("participant-moderation-state",
                        new ModerationSignal(studentId, normalizedAction, now), CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            // Durable moderation state must remain effective even when a live
            // SignalR connection is temporarily unavailable.
            logger.LogWarning(exception,
                "Unable to broadcast moderation action {Action} for {SessionId}/{StudentId}.",
                normalizedAction, sessionId, studentId);
        }

        return Results.Ok(record is null
            ? new LiveSessionModerationResponse(sessionId, studentId, false, false, null, null, null, now)
            : LiveSessionModerationResponse.From(record));
    }
}

public sealed record LiveSessionModerationResponse(
    Guid LiveSessionId,
    Guid StudentId,
    bool IsBlocked,
    bool IsMuted,
    DateTimeOffset? BlockedAt,
    DateTimeOffset? MutedAt,
    Guid? ChangedBy,
    DateTimeOffset UpdatedAt)
{
    public static LiveSessionModerationResponse From(LiveSessionParticipantModeration value) => new(
        value.LiveSessionId, value.StudentId, value.IsBlocked, value.IsMuted,
        value.BlockedAt, value.MutedAt, value.ChangedBy, value.UpdatedAt);
}
