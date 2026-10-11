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

public sealed record GradeAdjustmentResponse(Guid Id, Guid GradeEntryId, Guid StudentId,
    decimal PreviousScore, decimal NewScore, decimal MaxScore, string? PreviousFeedback,
    string? NewFeedback, string Reason, Guid AdjustedBy, DateTimeOffset AdjustedAt,
    int GradeVersion)
{
    public static GradeAdjustmentResponse From(GradeAdjustment adjustment) => new(adjustment.Id,
        adjustment.GradeEntryId, adjustment.StudentId, adjustment.PreviousScore,
        adjustment.NewScore, adjustment.MaxScore, adjustment.PreviousFeedback,
        adjustment.NewFeedback, adjustment.Reason, adjustment.AdjustedBy, adjustment.AdjustedAt,
        adjustment.GradeVersion);
}
