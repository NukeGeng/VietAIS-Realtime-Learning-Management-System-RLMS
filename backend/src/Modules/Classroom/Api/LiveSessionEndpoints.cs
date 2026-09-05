using System.Security.Cryptography;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
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

namespace LearningPlatform.Classroom.Api;

public static class LiveSessionEndpoints
{
    public static void MapLiveSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/classes/{classId:guid}/sessions", CreateSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
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
        endpoints.MapHub<LiveSessionHub>("/hubs/live/{sessionId:guid}")
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
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

        var teacherId = request.TeacherId ?? actor.UserId;
        var canManageClass = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
            StringComparer.Ordinal);
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

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
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
        }

        return Results.Ok(LiveSessionResponse.From(liveSession));
    }

    private static async Task<IResult> EndSessionAsync(
        Guid sessionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
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
            session.Update(liveSession);
            await session.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(LiveSessionResponse.From(liveSession));
    }

    private static async Task<IResult> JoinSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        IConfiguration configuration,
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

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "The live session has not started." });
        }

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        var presenceRegistered = await presence.MarkJoinedAsync(authenticatedActor.OrganizationId, sessionId,
            authenticatedActor.UserId,
            cancellationToken);
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

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        await presence.RemoveAsync(authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId,
            cancellationToken);
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

        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            return true;
        }

        return actor.Permissions.Contains(PermissionCatalog.ManageLiveSessions, StringComparer.Ordinal) &&
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId, cancellationToken);
    }

    private static async Task<bool> CanJoinAsync(
        AuthenticatedActor actor, LiveSession liveSession, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (actor.OrganizationId != liveSession.OrganizationId)
        {
            return false;
        }

        return await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                   actor.UserId, cancellationToken) ||
               await academics.IsStudentEnrolledAsync(actor.OrganizationId, liveSession.LearningClassId,
                   actor.UserId, cancellationToken);
    }
}

public sealed class LiveSessionHub(
    IDocumentStore store,
    IAcademicDirectory academics,
    LivePresenceStore presence,
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

        await using var session = store.QuerySession(actor!.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(sessionId);
        if (liveSession is null || liveSession.Status != LiveSessionStatus.Started ||
            !await CanJoinAsync(actor, liveSession))
        {
            Context.Abort();
            return;
        }

        var group = Group(actor.OrganizationId, sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await presence.MarkJoinedAsync(actor.OrganizationId, sessionId, actor.UserId);
        await Clients.Group(group).SendAsync("participant-joined",
            new PresenceDelta(actor.UserId, DateTimeOffset.UtcNow));
        await base.OnConnectedAsync();
    }

    public async Task RefreshPresence()
    {
        var principal = Context.User;
        if (principal is not null && TryGetSessionId(out var sessionId) &&
            AuthenticatedActor.TryCreate(principal, out var actor))
        {
            await using var session = store.QuerySession(actor!.OrganizationId.ToString("D"));
            var liveSession = await session.LoadAsync<LiveSession>(sessionId);
            if (liveSession is null || liveSession.Status != LiveSessionStatus.Started ||
                !await CanJoinAsync(actor, liveSession))
            {
                Context.Abort();
                return;
            }

            await presence.MarkJoinedAsync(actor.OrganizationId, sessionId, actor.UserId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var principal = Context.User;
        if (principal is not null && TryGetSessionId(out var sessionId) &&
            AuthenticatedActor.TryCreate(principal, out var actor))
        {
            await presence.RemoveAsync(actor!.OrganizationId, sessionId, actor.UserId);
            await Clients.Group(Group(actor.OrganizationId, sessionId)).SendAsync("participant-left",
                new PresenceDelta(actor.UserId, DateTimeOffset.UtcNow));
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

    private static string Group(Guid organizationId, Guid sessionId) =>
        $"tenant:{organizationId:D}:live-session:{sessionId:D}";

    private async Task<bool> CanJoinAsync(AuthenticatedActor actor, LiveSession liveSession)
    {
        return actor.OrganizationId == liveSession.OrganizationId &&
            (await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId) ||
             await academics.IsStudentEnrolledAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId));
    }
}

public sealed record CreateLiveSessionRequest(Guid? ClassScheduleId, Guid? TeacherId);

public sealed record LiveSessionResponse(Guid Id, Guid LearningClassId, Guid? ClassScheduleId,
    Guid TeacherId, LiveSessionStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt,
    string MediaRoomId)
{
    public static LiveSessionResponse From(LiveSession liveSession) => new(liveSession.Id,
        liveSession.LearningClassId, liveSession.ClassScheduleId, liveSession.TeacherId,
        liveSession.Status, liveSession.StartedAt, liveSession.EndedAt, liveSession.MediaRoomId);
}

public sealed record MediaAccessResponse(string ServerUrl, string RoomId, string AccessToken);

public sealed record LiveSessionJoinResponse(LiveSessionResponse Session, MediaAccessResponse Media,
    bool PresenceRegistered);

public sealed record PresenceDelta(Guid UserId, DateTimeOffset At);
