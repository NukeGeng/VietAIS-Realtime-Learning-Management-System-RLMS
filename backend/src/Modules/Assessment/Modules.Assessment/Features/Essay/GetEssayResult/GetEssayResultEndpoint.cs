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

namespace LearningPlatform.Assessment.Features.Essay.GetEssayResult;

internal static class GetEssayResultEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/essay-assignments/{assignmentId:guid}/groups/{groupId:guid}/result",
                GetEssayResultAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }


    private static async Task<IResult> GetEssayResultAsync(Guid assignmentId, Guid groupId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (assignment is null || group is null || group.EssayAssignmentId != assignmentId)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await IsActiveGroupMemberAsync(actor!, group, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var studentId = canManage
            ? group.Members.OrderBy(member => member.JoinedAt).Select(member => member.StudentId)
                .FirstOrDefault()
            : actor!.UserId;
        var entry = studentId == Guid.Empty
            ? null
            : await session.LoadAsync<GradeEntry>(EssayConstants.GradeEntryId(assignmentId, groupId,
                studentId), cancellationToken);
        return entry is null || entry.Status != GradeEntryStatus.Published
            ? Results.NotFound()
            : Results.Ok(GradeEntryResponse.From(entry));
    }

}
