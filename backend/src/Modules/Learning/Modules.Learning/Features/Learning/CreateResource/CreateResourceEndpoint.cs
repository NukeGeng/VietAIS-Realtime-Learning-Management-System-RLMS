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

namespace LearningPlatform.Learning.Features.Learning.CreateResource;

internal static class CreateResourceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/learning/classes/{classId:guid}/resources", CreateResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
    }


    private static async Task<IResult> CreateResourceAsync(Guid classId,
        CreateLearningResourceRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "Resources cannot be added to a completed or archived class." });
        }

        try
        {
            var resource = await BuildResourceAsync(request.LessonId, request.LiveSessionId,
                request.Title, request.Description, request.Type, request.ExternalUrl,
                request.DurationSeconds, classId, learningClass.CourseId, actor, session,
                cancellationToken);
            session.Insert(resource);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/learning/resources/{resource.Id:D}",
                LearningResourceResponse.From(resource));
        }
        catch (LearningResourceValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
