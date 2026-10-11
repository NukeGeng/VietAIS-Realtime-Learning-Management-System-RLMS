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

public sealed record GradebookCellResponse(Guid GradeItemId, decimal? Score, decimal MaxScore,
    decimal Weight, GradeEntryStatus? Status, string? Feedback, int? Version)
{
    public static GradebookCellResponse From(GradeItem item, GradeEntry? entry) =>
        entry is null ? new(item.Id, null, item.MaxScore, item.Weight, null, null, null) :
        new(item.Id, entry.Score, entry.MaxScore, item.Weight, entry.Status, entry.Feedback,
            entry.Version);
}
