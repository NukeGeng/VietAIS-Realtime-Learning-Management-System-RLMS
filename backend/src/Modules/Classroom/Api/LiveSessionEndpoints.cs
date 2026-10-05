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

public static class LiveSessionEndpoints
{
    public static void MapLiveSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/classes/{classId:guid}/sessions", CreateSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
        endpoints.MapGet("/api/classroom/classes/{classId:guid}/sessions", ListClassSessionsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}", GetSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/start", StartSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/end", EndSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/join", JoinSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/leave", LeaveSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/reconnect", ReconnectSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapHub<LiveSessionHub>("/hubs/live/{sessionId:guid}")
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }

    private static async Task<IResult> ListClassSessionsAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        bool? includeEnded,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canAccessClass = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canAccessClass)
        {
            return Results.Forbid();
        }

        var canReadAttendanceHistory = includeEnded == true &&
            actor.Permissions.Contains(PermissionCatalog.ManageAttendance, StringComparer.Ordinal);
        var sessionsQuery = session.Query<LiveSession>()
            .Where(value => value.OrganizationId == actor.OrganizationId &&
                            value.LearningClassId == classId &&
                            (value.Status == LiveSessionStatus.Scheduled ||
                             value.Status == LiveSessionStatus.Started ||
                             (canReadAttendanceHistory && value.Status == LiveSessionStatus.Ended)));
        if (!actor.Permissions.Contains(PermissionCatalog.ManageLiveSessions, StringComparer.Ordinal) &&
            !canReadAttendanceHistory)
        {
            sessionsQuery = sessionsQuery.Where(value => value.Status == LiveSessionStatus.Started);
        }

        var sessions = await sessionsQuery
            .OrderByDescending(value => value.Status)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(sessions.Select(LiveSessionResponse.From));
    }

    private static async Task<IResult> CreateSessionAsync(
        Guid classId,
        CreateLiveSessionRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot create a live session." });
        }

        if (request.LateAfterMinutes is < 0 or > 240)
        {
            return Results.BadRequest(new { error = "LateAfterMinutes must be between 0 and 240." });
        }

        var teacherId = request.TeacherId ?? actor.UserId;
        var canManageClass = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
            StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canManageClass && !await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, teacherId,
                cancellationToken))
        {
            return Results.BadRequest(new { error = "The session teacher must be assigned to the class." });
        }

        if (request.ClassScheduleId is { } scheduleId)
        {
            var schedule = await academics.FindScheduleAsync(actor.OrganizationId, classId, scheduleId,
                cancellationToken);
            if (schedule is null || schedule.Status is ScheduleStatus.Cancelled or ScheduleStatus.Completed)
            {
                return Results.Conflict(new { error = "The selected schedule is not available." });
            }

            if (schedule.TeacherId != teacherId)
            {
                return Results.BadRequest(new { error = "The session teacher must match the schedule teacher." });
            }
        }

        var now = DateTimeOffset.UtcNow;
        var liveSession = new LiveSession
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            LearningClassId = classId,
            ClassScheduleId = request.ClassScheduleId,
            TeacherId = teacherId,
            Status = LiveSessionStatus.Scheduled,
            LateAfterMinutes = request.LateAfterMinutes ?? 15,
            MediaRoomId = LiveSessionRules.RequireMediaRoomId($"rlms-{Guid.NewGuid():N}"),
            CreatedAt = now,
            UpdatedAt = now
        };
        session.Insert(liveSession);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/classroom/sessions/{liveSession.Id:D}",
            LiveSessionResponse.From(liveSession));
    }

    private static async Task<IResult> GetSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LiveSessionSnapshotCache snapshotCache,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            token => session.LoadAsync<LiveSession>(sessionId, token), cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var moderation = await session.LoadAsync<LiveSessionParticipantModeration>(
            ClassroomConstants.ModerationId(sessionId, actor.UserId), cancellationToken);
        if (moderation?.IsBlocked == true)
        {
            return Results.Forbid();
        }

        return Results.Ok(LiveSessionResponse.From(liveSession));
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

    private static async Task<IResult> JoinSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        LiveSessionSnapshotCache snapshotCache,
        IConfiguration configuration,
        IMessageBus messageBus,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            token => session.LoadAsync<LiveSession>(sessionId, token), cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.Status == LiveSessionStatus.Ended)
        {
            return Results.Conflict(new { error = "Live session has ended." });
        }

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "The live session has not started." });
        }

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var moderation = await session.LoadAsync<LiveSessionParticipantModeration>(
            ClassroomConstants.ModerationId(sessionId, actor.UserId), cancellationToken);
        if (moderation?.IsBlocked == true)
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        var joinedAt = DateTimeOffset.UtcNow;
        var presenceRegistered = await presence.MarkJoinedAsync(authenticatedActor.OrganizationId, sessionId,
            authenticatedActor.UserId,
            cancellationToken);
        if (await academics.IsStudentEnrolledAsync(authenticatedActor.OrganizationId,
                liveSession.LearningClassId, authenticatedActor.UserId, cancellationToken))
        {
            await AttendanceMessagePublisher.PublishParticipantJoinedAsync(messageBus, logger,
                authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId, joinedAt);
        }
        var mediaServerUrl = configuration["Media:ServerUrl"] ?? "";
        var accessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return Results.Ok(new LiveSessionJoinResponse(LiveSessionResponse.From(liveSession),
            new MediaAccessResponse(mediaServerUrl, liveSession.MediaRoomId, accessToken),
            presenceRegistered));
    }

    private static async Task<IResult> LeaveSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        LiveSessionSnapshotCache snapshotCache,
        IMessageBus messageBus,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            token => session.LoadAsync<LiveSession>(sessionId, token), cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        var leftAt = DateTimeOffset.UtcNow;
        await presence.RemoveAsync(authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId,
            cancellationToken);
        await AttendanceMessagePublisher.PublishParticipantLeftAsync(messageBus, logger,
            authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId, leftAt);
        return Results.NoContent();
    }

    private static async Task<IResult> ReconnectSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILogger<LiveSessionHub> logger,
        LiveSessionSnapshotCache snapshotCache,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            token => session.LoadAsync<LiveSession>(sessionId, token), cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.Status != LiveSessionStatus.Started ||
            !await CanJoinAsync(actor, liveSession, academics, cancellationToken))
        {
            return Results.Conflict(new { error = "The live session is no longer available." });
        }

        var moderation = await session.LoadAsync<LiveSessionParticipantModeration>(
            ClassroomConstants.ModerationId(sessionId, actor.UserId), cancellationToken);
        if (moderation?.IsBlocked == true)
        {
            return Results.Forbid();
        }

        if (await academics.IsStudentEnrolledAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId, cancellationToken))
        {
            await AttendanceMessagePublisher.PublishParticipantReconnectedAsync(messageBus, logger,
                actor.OrganizationId, sessionId, actor.UserId, DateTimeOffset.UtcNow);
        }

        return Results.NoContent();
    }

    private static async Task<bool> CanManageAsync(
        HttpContext context, LiveSession liveSession, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        return await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken);
    }

    private static async Task<bool> CanJoinAsync(
        AuthenticatedActor actor, LiveSession liveSession, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return await LiveSessionAuthorization.CanJoinAsync(actor, liveSession, academics, cancellationToken);
    }
}

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

public sealed record CreateLiveSessionRequest(Guid? ClassScheduleId, Guid? TeacherId,
    int? LateAfterMinutes);

public sealed record LiveSessionResponse(Guid Id, Guid LearningClassId, Guid? ClassScheduleId,
    Guid TeacherId, LiveSessionStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt,
    int LateAfterMinutes, string MediaRoomId)
{
    public static LiveSessionResponse From(LiveSession liveSession) => new(liveSession.Id,
        liveSession.LearningClassId, liveSession.ClassScheduleId, liveSession.TeacherId,
        liveSession.Status, liveSession.StartedAt, liveSession.EndedAt,
        liveSession.LateAfterMinutes, liveSession.MediaRoomId);
}

public sealed record LiveSessionStartedSignal(Guid SessionId, DateTimeOffset StartedAt);

public sealed record LiveSessionStateSignal(Guid SessionId, LiveSessionStatus Status,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

public sealed record MediaAccessResponse(string ServerUrl, string RoomId, string AccessToken);

public sealed record LiveSessionJoinResponse(LiveSessionResponse Session, MediaAccessResponse Media,
    bool PresenceRegistered);

public sealed record PresenceDelta(Guid UserId, DateTimeOffset At);

public sealed record PresenceParticipant(Guid UserId, string DisplayName, DateTimeOffset At);

public sealed record ModerationSignal(Guid TargetUserId, string Action, DateTimeOffset At);

public sealed record PresentationSignal(Guid PresenterId, bool PresenterIsTeacher, DateTimeOffset StartedAt)
{
    public static PresentationSignal From(LivePresentationLease lease) =>
        new(lease.UserId, lease.IsTeacher, lease.StartedAt);
}

public sealed record PresentationReplacementSignal(
    Guid PreviousPresenterId,
    Guid PresenterId,
    bool PresenterIsTeacher,
    DateTimeOffset At);

public sealed record PresentationStoppedSignal(Guid PresenterId, DateTimeOffset At);

public sealed record LiveSessionEndedSignal(Guid SessionId, DateTimeOffset EndedAt);

public sealed record PresentationReservationResponse(
    bool Acquired,
    Guid? PresenterId,
    bool PresenterIsTeacher)
{
    public static PresentationReservationResponse Busy(LivePresentationLease? lease) =>
        new(false, lease?.UserId, lease?.IsTeacher ?? false);

    public static PresentationReservationResponse AcquiredBy(LivePresentationLease lease) =>
        new(true, lease.UserId, lease.IsTeacher);
}

public sealed record MediaOfferSignal(Guid FromUserId, Guid ToUserId, string Sdp);

public sealed record MediaAnswerSignal(Guid FromUserId, Guid ToUserId, string Sdp);

public sealed record MediaIceCandidateRequest(string Candidate, string? SdpMid, int? SdpMLineIndex);

public sealed record MediaIceCandidateSignal(Guid FromUserId, Guid ToUserId, string Candidate,
    string? SdpMid, int? SdpMLineIndex);
