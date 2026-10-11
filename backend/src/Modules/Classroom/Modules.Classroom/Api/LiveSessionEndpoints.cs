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
        LearningPlatform.Classroom.Features.LiveSessions.CreateSession.CreateSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.ListClassSessions.ListClassSessionsEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.GetSession.GetSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.StartSession.StartSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.EndSession.EndSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.JoinSession.JoinSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.LeaveSession.LeaveSessionEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessions.ReconnectSession.ReconnectSessionEndpoint.Map(endpoints);
        endpoints.MapHub<LiveSessionHub>("/hubs/live/{sessionId:guid}")
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }

    internal static async Task<bool> CanManageAsync(
        HttpContext context, LiveSession liveSession, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        return await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken);
    }


    internal static async Task<bool> CanJoinAsync(
        AuthenticatedActor actor, LiveSession liveSession, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return await LiveSessionAuthorization.CanJoinAsync(actor, liveSession, academics, cancellationToken);
    }

}
