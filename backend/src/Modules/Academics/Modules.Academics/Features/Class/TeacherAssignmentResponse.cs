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

public sealed record TeacherAssignmentResponse(Guid Id, Guid LearningClassId, Guid TeacherId,
    TeacherAssignmentStatus Status, DateTimeOffset AssignedAt)
{
    public static TeacherAssignmentResponse From(ClassTeacherAssignment assignment) => new(
        assignment.Id, assignment.LearningClassId, assignment.TeacherId, assignment.Status,
        assignment.AssignedAt);
}
