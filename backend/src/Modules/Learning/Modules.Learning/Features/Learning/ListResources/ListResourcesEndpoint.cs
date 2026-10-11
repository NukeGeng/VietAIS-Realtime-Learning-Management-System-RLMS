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

namespace LearningPlatform.Learning.Features.Learning.ListResources;

internal static class ListResourcesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/resources", ListResourcesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningResources);
    }


    private static async Task<IResult> ListResourcesAsync(Guid classId, Guid? lessonId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
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

        var canManage = await CanManageClassAsync(actor, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<LearningResource>()
            .Where(resource => resource.LearningClassId == classId);
        if (lessonId is { } requestedLessonId)
        {
            query = query.Where(resource => resource.LessonId == requestedLessonId);
        }

        if (!canManage)
        {
            query = query.Where(resource => resource.Status == LearningResourceStatus.Published);
        }

        var resources = await query.OrderBy(resource => resource.CreatedAt)
            .ToListAsync(cancellationToken);
        if (!canManage)
        {
            var lessonIds = resources.Where(resource => resource.LessonId is not null)
                .Select(resource => resource.LessonId!.Value)
                .Distinct()
                .ToArray();
            if (lessonIds.Length > 0)
            {
                var publishedLessonIds = await session.Query<Lesson>()
                    .Where(lesson => lessonIds.Contains(lesson.Id) &&
                        lesson.Status == AcademicContentStatus.Published)
                    .Select(lesson => lesson.Id)
                    .ToListAsync(cancellationToken);
                resources = resources.Where(resource => resource.LessonId is null ||
                    publishedLessonIds.Contains(resource.LessonId.Value)).ToList();
            }
        }
        return Results.Ok(resources.Select(resource => LearningResourceResponse.From(resource)));
    }

}
