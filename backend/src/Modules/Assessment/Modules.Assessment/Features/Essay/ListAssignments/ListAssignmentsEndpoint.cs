using LearningPlatform.Assessment.Api;
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
using static LearningPlatform.Assessment.Api.EssayEndpoints;

namespace LearningPlatform.Assessment.Features.Essay.ListAssignments;

internal static class ListAssignmentsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/essay-assignments",
                ListAssignmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> ListAssignmentsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var canManage = await CanManageClassAsync(actor!, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var assignments = await session.Query<EssayAssignment>()
            .Where(assignment => assignment.LearningClassId == classId &&
                (canManage || assignment.Status != EssayAssignmentStatus.Draft))
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(assignments.Select(EssayAssignmentResponse.From));
    }

}
