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

namespace LearningPlatform.Assessment.Features.Essay.RequestTopicChange;

internal static class RequestTopicChangeEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/topic/change-request",
                RequestTopicChangeAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> RequestTopicChangeAsync(Guid groupId,
        RegisterEssayTopicRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var assignment = await session.LoadAsync<EssayAssignment>(group.EssayAssignmentId,
            cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active)
        {
            return Results.Conflict(new { error = "The essay assignment or group is not accepting topic changes." });
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            assignment.Id, group.Id), cancellationToken);
        if (topic is null || topic.Status is not (EssayTopicStatus.Approved or EssayTopicStatus.Locked) ||
            string.IsNullOrWhiteSpace(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "Only an approved or locked topic can be changed." });
        }

        string proposed;
        try
        {
            proposed = EssayRules.RequireTitle(request.ProposedTitle);
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }

        if (EssayRules.NormalizeTopic(proposed) == EssayRules.NormalizeTopic(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "The new topic must differ from the locked canonical topic." });
        }

        var hasPending = await session.Query<EssayTopicChangeRequest>()
            .Where(value => value.EssayTopicId == topic.Id &&
                value.Status == EssayTopicChangeRequestStatus.PendingReview)
            .AnyAsync(cancellationToken);
        if (hasPending)
        {
            return Results.Conflict(new { error = "A topic change is already awaiting teacher review." });
        }

        var now = DateTimeOffset.UtcNow;
        session.Insert(new EssayTopicChangeRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignment.Id,
            EssayGroupId = group.Id,
            EssayTopicId = topic.Id,
            PreviousCanonicalTitle = topic.CanonicalTitle!,
            PreviousStatus = topic.Status,
            ProposedTitle = proposed,
            Status = EssayTopicChangeRequestStatus.PendingReview,
            RequestedBy = actor.UserId,
            RequestedAt = now
        });
        topic.ProposedTitle = proposed;
        topic.CanonicalTitle = null;
        topic.Status = EssayTopicStatus.PendingReview;
        topic.ReviewedBy = null;
        topic.ReviewedAt = null;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

}
