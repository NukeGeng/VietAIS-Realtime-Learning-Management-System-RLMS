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

public sealed record RubricResponse(Guid Id, Guid ActivityId, RubricCriterion[] Criteria,
    DateTimeOffset UpdatedAt)
{
    public static RubricResponse From(AssessmentRubric rubric) => new(rubric.Id, rubric.ActivityId,
        rubric.Criteria, rubric.UpdatedAt);
}
