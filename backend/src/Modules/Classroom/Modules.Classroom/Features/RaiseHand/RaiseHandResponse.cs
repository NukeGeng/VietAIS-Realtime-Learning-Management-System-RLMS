using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Api;

public sealed record RaiseHandResponse(
    Guid Id,
    Guid LiveSessionId,
    Guid StudentId,
    RaiseHandStatus Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? LoweredAt,
    Guid? ClearedBy)
{
    public static RaiseHandResponse From(RaiseHand hand) => new(hand.Id, hand.LiveSessionId, hand.StudentId,
        hand.Status, hand.RaisedAt, hand.LoweredAt, hand.ClearedBy);
}
