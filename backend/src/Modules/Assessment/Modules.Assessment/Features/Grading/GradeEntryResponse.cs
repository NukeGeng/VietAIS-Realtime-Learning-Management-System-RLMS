using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Api;

public sealed record GradeEntryResponse(Guid Id, Guid ActivityId, Guid StudentId, int AttemptNumber,
    Guid GradeItemId, Guid? SourceSubmissionId, decimal Score, decimal MaxScore, GradeEntryStatus Status,
    GradeSource Source, bool IsComplete, string? Feedback, GradeQuestionResult[] QuestionResults,
    Guid? GradedBy, DateTimeOffset? GradedAt, DateTimeOffset? PublishedAt, bool? Passed, int Version)
{
    public static GradeEntryResponse From(GradeEntry entry) => new(entry.Id, entry.ActivityId,
        entry.StudentId, entry.AttemptNumber, entry.GradeItemId, entry.SourceSubmissionId,
        entry.Score, entry.MaxScore,
        entry.Status, entry.Source, entry.IsComplete, entry.Feedback, entry.QuestionResults,
        entry.GradedBy, entry.GradedAt, entry.PublishedAt, entry.Passed, entry.Version);
}
