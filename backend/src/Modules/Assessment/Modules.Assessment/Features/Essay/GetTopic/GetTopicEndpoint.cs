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

namespace LearningPlatform.Assessment.Features.Essay.GetTopic;

internal static class GetTopicEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/essay-groups/{groupId:guid}/topic", GetTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> GetTopicAsync(Guid groupId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            group.EssayAssignmentId, group.Id), cancellationToken);
        return topic is null ? Results.NotFound() : Results.Ok(EssayTopicResponse.From(topic));
    }

}
