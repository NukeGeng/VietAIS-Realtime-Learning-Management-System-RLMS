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

namespace LearningPlatform.Assessment.Features.Essay.ListSubmissions;

internal static class ListSubmissionsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/essay-groups/{groupId:guid}/submissions",
                ListSubmissionsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> ListSubmissionsAsync(Guid groupId, HttpContext context,
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

        var submissions = await session.Query<EssaySubmission>()
            .Where(submission => submission.EssayGroupId == groupId)
            .OrderByDescending(submission => submission.Version)
            .ToListAsync(cancellationToken);
        var validationIds = submissions.Where(value => value.ValidationResultId is not null)
            .Select(value => value.ValidationResultId!.Value).ToArray();
        var validationResults = validationIds.Length == 0
            ? []
            : await session.Query<ReportValidationResult>()
                .Where(value => validationIds.Contains(value.Id))
                .ToListAsync(cancellationToken);
        var errorsBySubmission = validationResults.ToDictionary(value => value.EssaySubmissionId,
            value => value.Errors);
        return Results.Ok(submissions.Select(submission => EssaySubmissionResponse.From(submission,
            errorsBySubmission.GetValueOrDefault(submission.Id, []))));
    }

}
