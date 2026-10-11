using LearningPlatform.Learning.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Learning.Storage;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Learning.Features.Learning.Shared.CurriculumContent;
using static LearningPlatform.Learning.Features.Learning.Shared.CourseProgress;
using static LearningPlatform.Learning.Features.Learning.Shared.ResourceValidation;
using static LearningPlatform.Learning.Features.Learning.Shared.LearningAccess;

namespace LearningPlatform.Learning.Features.Learning.GetResource;

internal static class GetResourceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/resources/{resourceId:guid}", GetResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningResources);
    }


    private static async Task<IResult> GetResourceAsync(Guid resourceId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, ILearningResourceStorage storage,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var resource = await session.LoadAsync<LearningResource>(resourceId, cancellationToken);
        if (resource is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, resource.LearningClassId, academics,
            cancellationToken);
        if (!canManage && (resource.Status != LearningResourceStatus.Published ||
                !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                    resource.LearningClassId, actor.UserId, cancellationToken) ||
                resource.LessonId is { } lessonId &&
                await session.LoadAsync<Lesson>(lessonId, cancellationToken) is not
                { Status: AcademicContentStatus.Published }))
        {
            return Results.Forbid();
        }

        string? accessUrl = resource.ExternalUrl;
        if (resource.StorageKey is { Length: > 0 } storageKey)
        {
            try
            {
                accessUrl = await storage.GetReadUrlAsync(actor!.OrganizationId, storageKey,
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        return Results.Ok(LearningResourceResponse.From(resource, accessUrl));
    }

}
