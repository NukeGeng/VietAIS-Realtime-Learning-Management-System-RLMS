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

namespace LearningPlatform.Assessment.Features.Essay.CreateGroup;

internal static class CreateGroupEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/groups",
                CreateGroupAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> CreateGroupAsync(Guid assignmentId,
        CreateEssayGroupRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        if (assignment.Status != EssayAssignmentStatus.Published ||
            !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, assignment.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var existing = await session.Query<EssayGroup>()
            .Where(group => group.EssayAssignmentId == assignmentId &&
                group.Members.Any(member => member.StudentId == actor.UserId) &&
                group.Status == EssayGroupStatus.Active)
            .AnyAsync(cancellationToken);
        if (existing)
        {
            return Results.Conflict(new { error = "A student can only belong to one active group for an assignment." });
        }

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
        {
            return Results.BadRequest(new { error = "A group name is required and must be at most 200 characters." });
        }

        var now = DateTimeOffset.UtcNow;
        var group = new EssayGroup
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignmentId,
            LearningClassId = assignment.LearningClassId,
            Name = name,
            Status = EssayGroupStatus.Active,
            Members = [new EssayGroupMember(actor.UserId, now)],
            CreatedAt = now,
            UpdatedAt = now
        };
        session.Insert(group);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/assessment/essay-groups/{group.Id:D}", EssayGroupResponse.From(group));
    }

}
