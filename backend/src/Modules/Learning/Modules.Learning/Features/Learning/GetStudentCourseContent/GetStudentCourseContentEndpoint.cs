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

namespace LearningPlatform.Learning.Features.Learning.GetStudentCourseContent;

internal static class GetStudentCourseContentEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/course-content",
                GetStudentCourseContentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static async Task<IResult> GetStudentCourseContentAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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
        if (course is null || course.OrganizationId != actor.OrganizationId ||
            course.Status != CourseStatus.Published)
        {
            return Results.NotFound();
        }

        var modules = await session.Query<CourseModule>()
            .Where(module => module.CourseId == course.Id &&
                module.OrganizationId == actor.OrganizationId &&
                module.Status == AcademicContentStatus.Published)
            .OrderBy(module => module.Order)
            .ThenBy(module => module.Id)
            .ToListAsync(cancellationToken);
        var moduleIds = modules.Select(module => module.Id).ToArray();
        var lessons = moduleIds.Length == 0
            ? new List<Lesson>()
            : await session.Query<Lesson>()
                .Where(lesson => lesson.CourseId == course.Id &&
                    lesson.OrganizationId == actor.OrganizationId &&
                    lesson.Status == AcademicContentStatus.Published &&
                    moduleIds.Contains(lesson.CourseModuleId))
                .OrderBy(lesson => lesson.CourseModuleId)
                .ThenBy(lesson => lesson.Order)
                .ThenBy(lesson => lesson.Id)
                .ToListAsync(cancellationToken);
        var lessonIds = lessons.Select(lesson => lesson.Id).ToArray();
        var records = lessonIds.Length == 0
            ? new List<StudentLessonProgressRecord>()
            : await session.Query<StudentLessonProgressRecord>()
                .Where(record => record.OrganizationId == actor.OrganizationId &&
                    record.LearningClassId == classId && record.StudentId == actor.UserId &&
                    lessonIds.Contains(record.LessonId))
                .ToListAsync(cancellationToken);

        var moduleViews = modules.Select(module =>
        {
            var moduleLessons = lessons.Where(lesson => lesson.CourseModuleId == module.Id)
                .Select(lesson =>
                {
                    var progress = records.FirstOrDefault(record => record.LessonId == lesson.Id);
                    return new StudentLessonSummaryResponse(lesson.Id, lesson.Title, lesson.Order,
                        progress?.Status);
                })
                .ToArray();
            return new StudentCourseModuleResponse(module.Id, module.Title, module.Order,
                moduleLessons.Count(lesson => lesson.Status == LessonProgressStatus.Completed),
                moduleLessons.Length, moduleLessons);
        }).ToArray();
        var completedLessons = moduleViews.Sum(module => module.CompletedLessons);
        return Results.Ok(new StudentCourseContentResponse(classId, course.Id, course.Title,
            completedLessons, lessons.Count,
            lessons.Count == 0 ? 0m : decimal.Round(completedLessons * 100m / lessons.Count, 2),
            moduleViews));
    }

}
