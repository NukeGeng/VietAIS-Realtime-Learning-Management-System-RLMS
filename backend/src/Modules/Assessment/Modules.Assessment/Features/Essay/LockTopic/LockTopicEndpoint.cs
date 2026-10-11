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

namespace LearningPlatform.Assessment.Features.Essay.LockTopic;

internal static class LockTopicEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-topics/{topicId:guid}/lock", LockTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> LockTopicAsync(Guid topicId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var topic = await session.LoadAsync<EssayTopic>(topicId, cancellationToken);
        if (topic is null)
        {
            return Results.NotFound();
        }

        var classId = await FindGroupClassAsync(session, topic.EssayGroupId, cancellationToken);
        if (!await CanManageClassAsync(actor!, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (topic.Status != EssayTopicStatus.Approved || string.IsNullOrWhiteSpace(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "Only an approved topic can be locked." });
        }

        topic.Status = EssayTopicStatus.Locked;
        topic.ReviewedBy = actor!.UserId;
        topic.ReviewedAt = DateTimeOffset.UtcNow;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

}
