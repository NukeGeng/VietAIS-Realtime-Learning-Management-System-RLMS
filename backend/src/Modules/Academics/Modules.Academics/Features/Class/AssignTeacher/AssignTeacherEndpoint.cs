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

namespace LearningPlatform.Academics.Features.Class.AssignTeacher;

internal static class AssignTeacherEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/teachers", AssignTeacherAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }


    private static async Task<IResult> AssignTeacherAsync(
        Guid classId,
        AssignTeacherRequest request,
        HttpContext context,
        IDocumentSession session,
        IIdentityDirectory identity,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

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

        var teacher = await identity.FindActiveMemberAsync(actor!.OrganizationId, request.TeacherId,
            cancellationToken);
        if (teacher is null || !await identity.HasPermissionAsync(actor.OrganizationId, request.TeacherId,
                PermissionCatalog.ManageClasses, cancellationToken))
        {
            return Results.BadRequest(new { error = "The assigned user is not an active teacher." });
        }

        var assignmentId = AcademicConstants.TeacherAssignmentId(classId, request.TeacherId);
        var assignment = await session.LoadAsync<ClassTeacherAssignment>(assignmentId, cancellationToken);
        if (assignment is not null && assignment.Status == TeacherAssignmentStatus.Active)
        {
            return Results.Conflict(new { error = "The teacher is already assigned to this class." });
        }

        var now = DateTimeOffset.UtcNow;
        if (assignment is null)
        {
            assignment = new ClassTeacherAssignment
            {
                Id = assignmentId,
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                TeacherId = request.TeacherId,
                Status = TeacherAssignmentStatus.Active,
                AssignedAt = now
            };
            session.Insert(assignment);
        }
        else
        {
            assignment.Status = TeacherAssignmentStatus.Active;
            assignment.AssignedAt = now;
            assignment.RemovedAt = null;
            session.Update(assignment);
        }

        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/academics/classes/{classId:D}/teachers/{request.TeacherId:D}",
            TeacherAssignmentResponse.From(assignment));
    }

}
