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

namespace LearningPlatform.Communication.Api;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/messages", ListAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/messages", SendAsync)
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

    private static async Task<IResult> SendAsync(
        Guid sessionId,
        SendChatMessageRequest request,
        HttpContext context,
        IDocumentSession session,
        LearningPlatform.Academics.Authorization.IAcademicDirectory academics,
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

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "Chat is available only while the live session is active." });
        }

        if (!await LiveSessionAuthorization.CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                LiveSessionId = sessionId,
                SenderId = actor.UserId,
                Body = ChatRules.RequireMessage(request.Body),
                SentAt = now
            };
            session.Insert(message);
            await session.SaveChangesAsync(cancellationToken);

            var response = ChatMessageResponse.From(message);
            try
            {
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("chat-message", response, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Persistence is the source of truth. A SignalR outage only delays
                // the live notification; clients can reload the message snapshot.
                logger.LogWarning(exception, "Unable to broadcast chat message for {SessionId}.", sessionId);
            }

            return Results.Created($"/api/classroom/sessions/{sessionId:D}/messages/{message.Id:D}", response);
        }
        catch (ChatValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

public sealed record SendChatMessageRequest(string? Body);

public sealed record ChatMessageResponse(
    Guid Id,
    Guid LiveSessionId,
    Guid SenderId,
    string Body,
    DateTimeOffset SentAt)
{
    public static ChatMessageResponse From(ChatMessage message) => new(message.Id, message.LiveSessionId,
        message.SenderId, message.Body, message.SentAt);
}
