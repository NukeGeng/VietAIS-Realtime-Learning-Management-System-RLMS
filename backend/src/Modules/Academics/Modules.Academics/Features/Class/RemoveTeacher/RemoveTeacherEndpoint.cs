using LearningPlatform.Academics.Api;
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
using static LearningPlatform.Academics.Api.ClassEndpoints;

namespace LearningPlatform.Academics.Features.Class.RemoveTeacher;

internal static class RemoveTeacherEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/academics/classes/{classId:guid}/teachers/{teacherId:guid}",
                RemoveTeacherAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }


    private static async Task<IResult> RemoveTeacherAsync(
        Guid classId,
        Guid teacherId,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(context, session, learningClass, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change its teachers." });
        }

        var assignment = await session.LoadAsync<ClassTeacherAssignment>(
            AcademicConstants.TeacherAssignmentId(classId, teacherId), cancellationToken);
        if (assignment is null || assignment.Status == TeacherAssignmentStatus.Removed)
        {
            return Results.NotFound();
        }

        assignment.Status = TeacherAssignmentStatus.Removed;
        assignment.RemovedAt = DateTimeOffset.UtcNow;
        session.Update(assignment);
        await session.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

}
