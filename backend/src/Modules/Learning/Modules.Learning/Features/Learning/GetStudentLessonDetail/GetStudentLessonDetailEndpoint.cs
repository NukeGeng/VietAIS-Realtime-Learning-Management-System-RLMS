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

namespace LearningPlatform.Learning.Features.Learning.GetStudentLessonDetail;

internal static class GetStudentLessonDetailEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/course-content/lessons/{lessonId:guid}",
                GetStudentLessonDetailAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static async Task<IResult> GetStudentLessonDetailAsync(Guid classId, Guid lessonId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        ILearningResourceStorage storage, CancellationToken cancellationToken)
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

        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var course = await session.LoadAsync<Course>(learningClass.CourseId, cancellationToken);
        var lesson = await session.LoadAsync<Lesson>(lessonId, cancellationToken);
        if (course is null || course.OrganizationId != actor.OrganizationId ||
            course.Status != CourseStatus.Published || lesson is null ||
            lesson.OrganizationId != actor.OrganizationId || lesson.CourseId != course.Id ||
            lesson.Status != AcademicContentStatus.Published)
        {
            return Results.NotFound();
        }

        var module = await session.LoadAsync<CourseModule>(lesson.CourseModuleId, cancellationToken);
        if (module is null || module.OrganizationId != actor.OrganizationId ||
            module.CourseId != course.Id || module.Status != AcademicContentStatus.Published)
        {
            return Results.NotFound();
        }

        var progressId = LearningConstants.LessonProgressId(lessonId, classId, actor.UserId);
        var progress = await session.LoadAsync<StudentLessonProgressRecord>(progressId,
            cancellationToken);
        var resources = await session.Query<LearningResource>()
            .Where(resource => resource.OrganizationId == actor.OrganizationId &&
                resource.CourseId == course.Id && resource.LearningClassId == classId &&
                resource.LessonId == lessonId &&
                resource.Status == LearningResourceStatus.Published)
            .OrderBy(resource => resource.CreatedAt)
            .ThenBy(resource => resource.Id)
            .ToListAsync(cancellationToken);
        var resourceViews = new List<StudentLessonResourceResponse>(resources.Count);
        foreach (var resource in resources)
        {
            string? accessUrl = resource.ExternalUrl;
            if (resource.StorageKey is { Length: > 0 } storageKey)
            {
                try
                {
                    accessUrl = await storage.GetReadUrlAsync(actor.OrganizationId, storageKey,
                        cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            }
            resourceViews.Add(StudentLessonResourceResponse.From(resource, accessUrl));
        }

        return Results.Ok(new StudentLessonDetailResponse(classId, course.Id, module.Id,
            module.Title, lesson.Id, lesson.Title, lesson.Order, lesson.ContentSummary,
            progress?.Status, progress?.StartedAt, progress?.CompletedAt,
            resourceViews.ToArray()));
    }

}
