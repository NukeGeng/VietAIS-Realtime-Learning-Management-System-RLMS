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

namespace LearningPlatform.Assessment.Features.Essay.RegisterTopic;

internal static class RegisterTopicEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/topic", RegisterTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> RegisterTopicAsync(Guid groupId, RegisterEssayTopicRequest request,
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

        string proposed;
        try
        {
            proposed = EssayRules.RequireTitle(request.ProposedTitle);
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        var topicId = EssayConstants.TopicId(assignment.Id, group.Id);
        var topic = await session.LoadAsync<EssayTopic>(topicId, cancellationToken);
        if (topic is not null && topic.Status is (EssayTopicStatus.Approved or EssayTopicStatus.Locked))
        {
            return Results.Conflict(new { error = "An approved topic must use the topic change workflow." });
        }

        var now = DateTimeOffset.UtcNow;
        topic ??= new EssayTopic
        {
            Id = topicId,
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignment.Id,
            EssayGroupId = group.Id
        };
        topic.ProposedTitle = proposed;
        topic.CanonicalTitle = null;
        topic.Status = EssayTopicStatus.PendingReview;
        topic.SubmittedAt = now;
        topic.ReviewedBy = null;
        topic.ReviewedAt = null;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

}
