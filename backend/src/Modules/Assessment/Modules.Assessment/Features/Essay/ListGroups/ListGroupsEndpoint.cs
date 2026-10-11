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

namespace LearningPlatform.Assessment.Features.Essay.ListGroups;

internal static class ListGroupsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/essay-assignments/{assignmentId:guid}/groups",
                ListGroupsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> ListGroupsAsync(Guid assignmentId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                assignment.LearningClassId, actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<EssayGroup>().Where(group => group.EssayAssignmentId == assignmentId);
        if (!canManage)
        {
            query = query.Where(group => group.Members.Any(member => member.StudentId == actor!.UserId));
        }

        var groups = await query.OrderBy(group => group.Name).ToListAsync(cancellationToken);
        return Results.Ok(groups.Select(EssayGroupResponse.From));
    }

}
