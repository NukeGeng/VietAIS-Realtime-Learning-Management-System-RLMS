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

public sealed record AttendanceRecordResponse(
    Guid Id,
    Guid LiveSessionId,
    Guid StudentId,
    DateTimeOffset? FirstJoinedAt,
    DateTimeOffset? LastLeftAt,
    TimeSpan TotalDuration,
    AttendanceStatus Status,
    Guid? AdjustedBy,
    string? AdjustmentReason,
    int ReconnectCount,
    IReadOnlyList<AttendanceSegmentResponse> Segments)
{
    public string? StudentDisplayName { get; init; }
    public DateTimeOffset? LateCutoffAt { get; init; }

    public static AttendanceRecordResponse From(AttendanceRecord record) => new(record.Id,
        record.LiveSessionId, record.StudentId, record.FirstJoinedAt, record.LastLeftAt,
        record.TotalDuration, record.Status, record.AdjustedBy, record.AdjustmentReason,
        record.ReconnectCount,
        (record.Segments ?? []).Select(AttendanceSegmentResponse.From).ToArray())
    {
        LateCutoffAt = record.LateCutoffAt
    };
}
