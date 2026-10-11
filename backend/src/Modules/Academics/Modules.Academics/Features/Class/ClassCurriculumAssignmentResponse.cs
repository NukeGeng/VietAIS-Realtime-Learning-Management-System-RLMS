using JasperFx;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Academics.Api;

public sealed record ClassCurriculumAssignmentResponse(Guid Id, Guid LearningClassId,
    Guid CurriculumId, string Title, string Description, int Order, ClassCurriculumAssignmentStatus Status)
{
    public static ClassCurriculumAssignmentResponse From(ClassCurriculumAssignment assignment,
        Curriculum curriculum) => new(assignment.Id, assignment.LearningClassId,
        curriculum.Id, curriculum.Title, curriculum.Description, assignment.Order,
        assignment.Status);
}
