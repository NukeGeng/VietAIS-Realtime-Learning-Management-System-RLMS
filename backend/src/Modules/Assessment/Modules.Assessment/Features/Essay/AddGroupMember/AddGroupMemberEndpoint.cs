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

namespace LearningPlatform.Assessment.Features.Essay.AddGroupMember;

internal static class AddGroupMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/members",
                AddGroupMemberAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> AddGroupMemberAsync(Guid groupId, AddEssayGroupMemberRequest request,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(group.EssayAssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken);
        var isMember = await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken);
        if (!canManage && !isMember)
        {
            return Results.Forbid();
        }

        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active || group.Members.Count >= assignment.MaxGroupSize)
        {
            return Results.Conflict(new { error = "The group cannot accept another member." });
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            assignment.Id, group.Id), cancellationToken);
        var hasSubmission = await session.Query<EssaySubmission>()
            .Where(value => value.EssayGroupId == group.Id &&
                value.Status != EssaySubmissionStatus.Rejected)
            .AnyAsync(cancellationToken);
        if (topic?.Status == EssayTopicStatus.Locked || hasSubmission)
        {
            return Results.Conflict(new { error = "Group membership is locked after the topic or report is finalized." });
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, group.LearningClassId,
                request.StudentId, cancellationToken))
        {
            return Results.BadRequest(new { error = "The student is not actively enrolled in this class." });
        }

        var inAnotherGroup = await session.Query<EssayGroup>()
            .Where(value => value.EssayAssignmentId == group.EssayAssignmentId && value.Id != group.Id &&
                value.Status == EssayGroupStatus.Active &&
                value.Members.Any(member => member.StudentId == request.StudentId))
            .AnyAsync(cancellationToken);
        if (inAnotherGroup || group.Members.Any(member => member.StudentId == request.StudentId))
        {
            return Results.Conflict(new { error = "The student is already in a group for this assignment." });
        }

        group.Members.Add(new EssayGroupMember(request.StudentId, DateTimeOffset.UtcNow));
        group.UpdatedAt = DateTimeOffset.UtcNow;
        session.Store(group);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayGroupResponse.From(group));
    }

}
