using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Api;

public sealed record HelpRequestResponse(Guid AttemptId, Guid StudentId, Guid ActivityId,
    string ActivityTitle, Guid? CurrentQuestionId, StudentActivityStatus Status,
    DateTimeOffset? RequestedAt, DateTimeOffset? AcknowledgedAt, DateTimeOffset LastActivityAt,
    HelpRequestStatus HelpStatus, DateTimeOffset? ResolvedAt, Guid? HandledBy,
    double WaitDurationSeconds)
{
    public string? StudentDisplayName { get; init; }

    public static HelpRequestResponse From(HelpRequest request, StudentActivity attempt,
        AssessmentActivity activity)
    {
        var waitEnd = request.AcknowledgedAt ?? request.ResolvedAt ?? DateTimeOffset.UtcNow;
        var waitDurationSeconds = Math.Max(0, (waitEnd - request.RequestedAt).TotalSeconds);
        return new(request.StudentActivityId, request.StudentId, request.ActivityId, activity.Title,
            attempt.CurrentQuestionId, attempt.Status, request.RequestedAt,
            request.AcknowledgedAt, attempt.LastActivityAt, request.Status, request.ResolvedAt,
            request.HandledBy, waitDurationSeconds);
    }
}
