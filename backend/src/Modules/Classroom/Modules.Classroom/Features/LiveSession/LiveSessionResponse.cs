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

public sealed record LiveSessionResponse(Guid Id, Guid LearningClassId, Guid? ClassScheduleId,
    Guid TeacherId, LiveSessionStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt,
    int LateAfterMinutes, string MediaRoomId)
{
    public static LiveSessionResponse From(LiveSession liveSession) => new(liveSession.Id,
        liveSession.LearningClassId, liveSession.ClassScheduleId, liveSession.TeacherId,
        liveSession.Status, liveSession.StartedAt, liveSession.EndedAt,
        liveSession.LateAfterMinutes, liveSession.MediaRoomId);
}
