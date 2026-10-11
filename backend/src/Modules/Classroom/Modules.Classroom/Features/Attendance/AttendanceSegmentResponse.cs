using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Api;

public sealed record AttendanceSegmentResponse(
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt,
    TimeSpan Duration)
{
    public static AttendanceSegmentResponse From(AttendanceSegment segment) =>
        new(segment.JoinedAt, segment.LeftAt, segment.Duration);
}
