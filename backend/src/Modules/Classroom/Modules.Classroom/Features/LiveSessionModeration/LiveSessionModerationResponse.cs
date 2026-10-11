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
