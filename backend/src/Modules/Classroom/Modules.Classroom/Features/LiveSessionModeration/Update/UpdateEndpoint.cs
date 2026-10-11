using LearningPlatform.Classroom.Api;
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
using static LearningPlatform.Classroom.Api.LiveSessionModerationEndpoints;

namespace LearningPlatform.Classroom.Features.LiveSessionModeration.Update;

internal static class UpdateEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/moderation/{studentId:guid}/{action}", UpdateAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
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
