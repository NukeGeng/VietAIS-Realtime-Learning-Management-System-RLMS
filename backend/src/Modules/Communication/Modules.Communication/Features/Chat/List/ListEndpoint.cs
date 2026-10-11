using LearningPlatform.Communication.Api;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static LearningPlatform.Communication.Api.ChatEndpoints;

namespace LearningPlatform.Communication.Features.Chat.List;

internal static class ListEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/messages", ListAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }


    private static async Task<IResult> ListAsync(
        Guid sessionId,
        int? limit,
        DateTimeOffset? before,
        HttpContext context,
        IQuerySession session,
        LearningPlatform.Academics.Authorization.IAcademicDirectory academics,
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

        if (!await LiveSessionAuthorization.CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var take = limit is null or <= 0 or > 100 ? 50 : limit.Value;
        var query = session.Query<ChatMessage>()
            .Where(message => message.LiveSessionId == sessionId);
        if (before is { } cursor)
        {
            query = query.Where(message => message.SentAt < cursor);
        }

        var messages = await query.OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Results.Ok(messages.AsEnumerable().Reverse().Select(ChatMessageResponse.From).ToArray());
    }

}
