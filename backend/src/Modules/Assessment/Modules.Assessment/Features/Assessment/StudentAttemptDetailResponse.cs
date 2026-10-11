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

public sealed record StudentAttemptDetailResponse(Guid Id, Guid OrganizationId, Guid StudentId,
    int AttemptNumber, Guid ActivityId, Guid LearningClassId, Guid? LiveSessionId, Guid? CurrentQuestionId,
    Guid[] QuestionOrder, StudentActivityStatus Status, Dictionary<Guid, string> Answers, int AnsweredCount,
    int TotalQuestions, decimal Progress, DateTimeOffset StartedAt, DateTimeOffset LastActivityAt,
    DateTimeOffset? SubmittedAt, long Version, bool HelpRequested = false,
    DateTimeOffset? HelpRequestedAt = null, DateTimeOffset? HelpAcknowledgedAt = null)
{
    public string? StudentDisplayName { get; init; }

    public static StudentAttemptDetailResponse From(StudentAttemptDetailView detail) => new(detail.Id,
        detail.OrganizationId, detail.StudentId, detail.AttemptNumber, detail.ActivityId,
        detail.LearningClassId,
        detail.LiveSessionId, detail.CurrentQuestionId, detail.QuestionOrder, detail.Status,
        new Dictionary<Guid, string>(detail.Answers), detail.AnsweredCount, detail.TotalQuestions,
        detail.Progress, detail.StartedAt, detail.LastActivityAt, detail.SubmittedAt, detail.Version,
        detail.HelpRequested, detail.HelpRequestedAt, detail.HelpAcknowledgedAt);
}
