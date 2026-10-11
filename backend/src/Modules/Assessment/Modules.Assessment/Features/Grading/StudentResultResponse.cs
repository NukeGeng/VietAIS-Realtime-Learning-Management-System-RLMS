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

public sealed record StudentResultResponse(Guid ActivityId, Guid GradeEntryId, decimal Score,
    decimal MaxScore, string? Feedback, GradeQuestionResult[] QuestionResults,
    DateTimeOffset PublishedAt, bool? Passed, int Version)
{
    public static StudentResultResponse From(GradeEntry entry) => new(entry.ActivityId, entry.Id,
        entry.Score, entry.MaxScore, entry.Feedback, entry.QuestionResults, entry.PublishedAt!.Value,
        entry.Passed, entry.Version);
}
