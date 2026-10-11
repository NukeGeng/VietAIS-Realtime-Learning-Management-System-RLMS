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

public sealed record GradebookItemResponse(Guid GradeItemId, Guid ActivityId, Guid? EssayAssignmentId,
    string Title, decimal MaxScore, decimal Weight)
{
    public static GradebookItemResponse From(GradeItem item) => new(item.Id, item.ActivityId,
        item.EssayAssignmentId, item.Title, item.MaxScore, item.Weight);
}
