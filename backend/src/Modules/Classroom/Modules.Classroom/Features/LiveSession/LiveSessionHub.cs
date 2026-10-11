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

namespace LearningPlatform.Classroom.Api;

public sealed class LiveSessionHub(
    IDocumentStore store,
    IAcademicDirectory academics,
    IIdentityDirectory identity,
    LivePresenceStore presence,
    LivePresentationStore presentation,
    LiveSessionSnapshotCache snapshotCache,
    IMessageBus messageBus,
    ILogger<LiveSessionHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var principal = Context.User;
        if (principal is null || !TryGetSessionId(out var sessionId))
        {
            Context.Abort();
            return;
        }

        if (!AuthenticatedActor.TryCreate(principal, out var actor))
        {
            Context.Abort();
            return;
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            async _ =>
            {
                await using var session = store.QuerySession(actor.OrganizationId.ToString("D"));
                return await session.LoadAsync<LiveSession>(sessionId);
            }, Context.ConnectionAborted);
        if (liveSession is null || (liveSession.Status != LiveSessionStatus.Scheduled &&
                liveSession.Status != LiveSessionStatus.Started) ||
            !await CanJoinAsync(actor, liveSession) || await IsBlockedAsync(actor, liveSession))
        {
            Context.Abort();
            return;
        }

        var group = Group(actor.OrganizationId, sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        if (liveSession.Status == LiveSessionStatus.Scheduled)
        {
            await Clients.Caller.SendAsync("session-state",
                new LiveSessionStateSignal(liveSession.Id, liveSession.Status,
                    liveSession.StartedAt, liveSession.EndedAt), Context.ConnectionAborted);
            await base.OnConnectedAsync();
            return;
        }

        await presence.MarkJoinedAsync(actor.OrganizationId, sessionId, actor.UserId,
            connectionId: Context.ConnectionId);
        var moderation = await LoadModerationAsync(actor.OrganizationId, sessionId, actor.UserId);
        if (moderation?.IsMuted == true)
        {
            await Clients.Caller.SendAsync("participant-moderation-state",
                new ModerationSignal(actor.UserId, "mute", DateTimeOffset.UtcNow),
                Context.ConnectionAborted);
        }
        var currentPresentation = await presentation.GetAsync(actor.OrganizationId, sessionId,
            Context.ConnectionAborted);
        if (currentPresentation is not null)
        {
            await Clients.Caller.SendAsync("presentation-started",
                PresentationSignal.From(currentPresentation), Context.ConnectionAborted);
        }
        var member = await identity.FindActiveMemberAsync(actor.OrganizationId, actor.UserId,
            Context.ConnectionAborted);
        if (await academics.IsStudentEnrolledAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId))
        {
            await AttendanceMessagePublisher.PublishParticipantJoinedAsync(messageBus, logger,
                actor.OrganizationId, sessionId, actor.UserId, DateTimeOffset.UtcNow, Context.ConnectionId);
        }
        await Clients.Group(group).SendAsync("participant-joined",
            new PresenceDelta(actor.UserId, DateTimeOffset.UtcNow));
        if (member is not null)
        {
            await Clients.Group(group).SendAsync("participant-presence-joined",
                new PresenceParticipant(member.Id, member.DisplayName, DateTimeOffset.UtcNow));
        }
        await base.OnConnectedAsync();
    }

    public async Task RefreshPresence()
    {
        var principal = Context.User;
        if (principal is not null && TryGetSessionId(out var sessionId) &&
            AuthenticatedActor.TryCreate(principal, out var actor))
        {
            var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
                async _ =>
                {
                    await using var session = store.QuerySession(actor.OrganizationId.ToString("D"));
                    return await session.LoadAsync<LiveSession>(sessionId);
                }, Context.ConnectionAborted);
            if (liveSession is null || (liveSession.Status != LiveSessionStatus.Scheduled &&
                    liveSession.Status != LiveSessionStatus.Started) ||
                !await CanJoinAsync(actor, liveSession))
            {
                Context.Abort();
                return;
            }

            if (liveSession.Status == LiveSessionStatus.Started)
            {
                await presence.MarkJoinedAsync(actor.OrganizationId, sessionId, actor.UserId,
                    connectionId: Context.ConnectionId);
            }
        }
    }

    /// <summary>
    /// Returns the current participant IDs to the caller so a newly connected
    /// browser can establish WebRTC peer connections in either direction.
    /// Only signaling metadata travels through SignalR; media stays on the
    /// WebRTC media path.
    /// </summary>
    public async Task RequestMediaPeers()
    {
        var participant = await RequireParticipantAsync();
        var users = await presence.GetUsersAsync(participant.Actor.OrganizationId,
            participant.Session.Id, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("participant-snapshot",
            users.Where(userId => userId != participant.Actor.UserId), Context.ConnectionAborted);
        var members = new List<PresenceParticipant>(users.Count);
        foreach (var userId in users)
        {
            var member = await identity.FindActiveMemberAsync(participant.Actor.OrganizationId, userId,
                Context.ConnectionAborted);
            if (member is not null)
            {
                members.Add(new PresenceParticipant(member.Id, member.DisplayName, DateTimeOffset.UtcNow));
            }
        }
        await Clients.Caller.SendAsync("participant-presence-snapshot", members,
            Context.ConnectionAborted);
    }

    public async Task SendMediaOffer(Guid targetUserId, string sdp)
    {
        var participant = await RequireParticipantAsync();
        RequireSignalPayload(targetUserId, sdp, maxLength: 256_000);
        await Clients.Group(Group(participant.Actor.OrganizationId, participant.Session.Id))
            .SendAsync("media-offer",
                new MediaOfferSignal(participant.Actor.UserId, targetUserId, sdp),
                Context.ConnectionAborted);
    }

    public async Task SendMediaAnswer(Guid targetUserId, string sdp)
    {
        var participant = await RequireParticipantAsync();
        RequireSignalPayload(targetUserId, sdp, maxLength: 256_000);
        await Clients.Group(Group(participant.Actor.OrganizationId, participant.Session.Id))
            .SendAsync("media-answer",
                new MediaAnswerSignal(participant.Actor.UserId, targetUserId, sdp),
                Context.ConnectionAborted);
    }

    public async Task SendMediaIceCandidate(Guid targetUserId, MediaIceCandidateRequest candidate)
    {
        var participant = await RequireParticipantAsync();
        if (targetUserId == Guid.Empty || candidate is null || string.IsNullOrWhiteSpace(candidate.Candidate) ||
            candidate.Candidate.Length > 16_000)
        {
            throw new HubException("Invalid media ICE candidate.");
        }

        await Clients.Group(Group(participant.Actor.OrganizationId, participant.Session.Id))
            .SendAsync("media-ice-candidate",
                new MediaIceCandidateSignal(participant.Actor.UserId, targetUserId, candidate.Candidate,
                    candidate.SdpMid, candidate.SdpMLineIndex),
                Context.ConnectionAborted);
    }

    public async Task<PresentationReservationResponse> RequestPresentation(bool takeover = false)
    {
        var participant = await RequireParticipantAsync();
        var isTeacher = participant.Actor.Permissions.Contains(
            PermissionCatalog.ManageLiveSessions, StringComparer.Ordinal);
        if (takeover && !isTeacher)
        {
            throw new HubException("Only the teacher can take over presentation.");
        }

        var result = await presentation.TryAcquireAsync(
            participant.Actor.OrganizationId,
            participant.Session.Id,
            participant.Actor.UserId,
            isTeacher,
            Context.ConnectionId,
            takeover && isTeacher,
            Context.ConnectionAborted);
        if (!result.Acquired)
        {
            return PresentationReservationResponse.Busy(result.Current);
        }

        var group = Group(participant.Actor.OrganizationId, participant.Session.Id);
        if (result.Replaced is not null && result.Replaced.UserId != participant.Actor.UserId)
        {
            await Clients.Group(group).SendAsync("presentation-replaced",
                new PresentationReplacementSignal(result.Replaced.UserId, participant.Actor.UserId,
                    isTeacher, DateTimeOffset.UtcNow), CancellationToken.None);
        }

        var lease = result.Current ?? new LivePresentationLease(
            participant.Actor.UserId, isTeacher, Context.ConnectionId, DateTimeOffset.UtcNow);
        await Clients.Group(group).SendAsync("presentation-started",
            PresentationSignal.From(lease), Context.ConnectionAborted);
        return PresentationReservationResponse.AcquiredBy(lease);
    }

    public async Task ReleasePresentation()
    {
        var participant = await RequireParticipantAsync();
        if (await presentation.ReleaseAsync(participant.Actor.OrganizationId, participant.Session.Id,
                participant.Actor.UserId, Context.ConnectionId, Context.ConnectionAborted))
        {
            await Clients.Group(Group(participant.Actor.OrganizationId, participant.Session.Id))
                .SendAsync("presentation-stopped",
                    new PresentationStoppedSignal(participant.Actor.UserId, DateTimeOffset.UtcNow),
                    Context.ConnectionAborted);
        }
    }

    public async Task RefreshPresentation()
    {
        var participant = await RequireParticipantAsync();
        await presentation.RefreshAsync(participant.Actor.OrganizationId, participant.Session.Id,
            participant.Actor.UserId, Context.ConnectionId, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var principal = Context.User;
        if (principal is not null && TryGetSessionId(out var sessionId) &&
            AuthenticatedActor.TryCreate(principal, out var actor))
        {
            var leftAt = DateTimeOffset.UtcNow;
            var stoppedPresentation = await presentation.ReleaseAsync(actor!.OrganizationId, sessionId,
                actor.UserId, Context.ConnectionId);
            var wasLastConnection = await presence.RemoveAsync(actor!.OrganizationId, sessionId, actor.UserId,
                connectionId: Context.ConnectionId);
            if (stoppedPresentation)
            {
                await Clients.Group(Group(actor.OrganizationId, sessionId)).SendAsync("presentation-stopped",
                    new PresentationStoppedSignal(actor.UserId, leftAt), CancellationToken.None);
            }
            await AttendanceMessagePublisher.PublishParticipantLeftAsync(messageBus, logger,
                actor.OrganizationId, sessionId, actor.UserId, leftAt, Context.ConnectionId);
            if (wasLastConnection)
            {
                await Clients.Group(Group(actor.OrganizationId, sessionId)).SendAsync("participant-left",
                    new PresenceDelta(actor.UserId, DateTimeOffset.UtcNow));
            }
        }

        if (exception is not null)
        {
            logger.LogDebug(exception, "Live session connection {ConnectionId} disconnected with an error.",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private bool TryGetSessionId(out Guid sessionId)
    {
        var httpContext = Context.GetHttpContext();
        if (Guid.TryParse(httpContext?.Request.RouteValues["sessionId"]?.ToString(), out sessionId) &&
            sessionId != Guid.Empty)
        {
            return true;
        }

        foreach (var segment in httpContext?.Request.Path.Value?.Split('/',
                     StringSplitOptions.RemoveEmptyEntries).Reverse() ?? [])
        {
            if (Guid.TryParse(segment, out sessionId) && sessionId != Guid.Empty)
            {
                return true;
            }
        }

        sessionId = Guid.Empty;
        return false;
    }

    private async Task<(AuthenticatedActor Actor, LiveSession Session)> RequireParticipantAsync()
    {
        if (!TryGetSessionId(out var sessionId) || Context.User is null ||
            !AuthenticatedActor.TryCreate(Context.User, out var actor))
        {
            Context.Abort();
            throw new HubException("Authentication is required.");
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            async _ =>
            {
                await using var session = store.QuerySession(actor.OrganizationId.ToString("D"));
                return await session.LoadAsync<LiveSession>(sessionId);
            }, Context.ConnectionAborted);
        if (liveSession is null || liveSession.Status != LiveSessionStatus.Started ||
            !await CanJoinAsync(actor, liveSession) || await IsBlockedAsync(actor, liveSession))
        {
            Context.Abort();
            throw new HubException("The live session is no longer available.");
        }

        return (actor, liveSession);
    }

    private static void RequireSignalPayload(Guid targetUserId, string sdp, int maxLength)
    {
        if (targetUserId == Guid.Empty || string.IsNullOrWhiteSpace(sdp) || sdp.Length > maxLength)
        {
            throw new HubException("Invalid media signaling payload.");
        }
    }

    public static string Group(Guid organizationId, Guid sessionId) =>
        $"tenant:{organizationId:D}:live-session:{sessionId:D}";

    private async Task<bool> CanJoinAsync(AuthenticatedActor actor, LiveSession liveSession)
    {
        return await LiveSessionAuthorization.CanJoinAsync(actor, liveSession, academics);
    }

    private async Task<bool> IsBlockedAsync(AuthenticatedActor actor, LiveSession liveSession)
    {
        var moderation = await LoadModerationAsync(actor.OrganizationId, liveSession.Id, actor.UserId);
        return moderation?.IsBlocked == true;
    }

    private async Task<LiveSessionParticipantModeration?> LoadModerationAsync(
        Guid organizationId, Guid sessionId, Guid studentId)
    {
        await using var session = store.QuerySession(organizationId.ToString("D"));
        return await session.LoadAsync<LiveSessionParticipantModeration>(
            ClassroomConstants.ModerationId(sessionId, studentId), Context.ConnectionAborted);
    }
}
