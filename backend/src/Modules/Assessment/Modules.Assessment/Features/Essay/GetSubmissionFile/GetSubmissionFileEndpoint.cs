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

namespace LearningPlatform.Assessment.Features.Essay.GetSubmissionFile;

internal static class GetSubmissionFileEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/essay-submissions/{submissionId:guid}/file",
                GetSubmissionFileAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }


    private static async Task<IResult> GetSubmissionFileAsync(Guid submissionId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        IFileStorage storage, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var submission = await session.LoadAsync<EssaySubmission>(submissionId, cancellationToken);
        if (submission is null || submission.FileAssetId == Guid.Empty)
        {
            return Results.NotFound();
        }

        var group = await session.LoadAsync<EssayGroup>(submission.EssayGroupId, cancellationToken);
        var assignment = await session.LoadAsync<EssayAssignment>(submission.EssayAssignmentId,
            cancellationToken);
        if (group is null || assignment is null || group.EssayAssignmentId != assignment.Id)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, group.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await IsActiveGroupMemberAsync(actor!, group, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var asset = await session.LoadAsync<FileAsset>(submission.FileAssetId, cancellationToken);
        if (asset is null || asset.OrganizationId != actor!.OrganizationId ||
            string.IsNullOrWhiteSpace(asset.StorageKey))
        {
            return Results.NotFound();
        }

        try
        {
            var accessUrl = await storage.GetReadUrlAsync(actor.OrganizationId, asset.StorageKey,
                cancellationToken);
            return accessUrl is null
                ? Results.NotFound()
                : Results.Ok(new EssaySubmissionFileResponse(submission.Id, asset.FileName,
                    asset.ContentType, asset.Size, accessUrl));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

}
