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
