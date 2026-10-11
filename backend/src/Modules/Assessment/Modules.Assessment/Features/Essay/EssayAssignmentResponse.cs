using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
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

public sealed record EssayAssignmentResponse(Guid Id, Guid LearningClassId, Guid? CourseId, string Title,
    string Description, DateTimeOffset? Deadline, int MaxScore, int MaxGroupSize,
    bool AllowResubmission, EssayAssignmentStatus Status, ReportValidationPolicy ValidationPolicy)
{
    public static EssayAssignmentResponse From(EssayAssignment assignment) => new(assignment.Id,
        assignment.LearningClassId, assignment.CourseId, assignment.Title, assignment.Description,
        assignment.Deadline, assignment.MaxScore, assignment.MaxGroupSize, assignment.AllowResubmission,
        assignment.Status, assignment.ValidationPolicy);
}
