using JasperFx;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Academics.Api;

public static class AcademicEndpoints
{
    public static void MapAcademicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Academics.Features.Academic.ListCourses.ListCoursesEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.GetCourse.GetCourseEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.CreateCourse.CreateCourseEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.UpdateCourse.UpdateCourseEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.PublishCourse.PublishCourseEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.ArchiveCourse.ArchiveCourseEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.ListCurriculums.ListCurriculumsEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.CreateCurriculum.CreateCurriculumEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.PublishCurriculum.PublishCurriculumEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.CreateModule.CreateModuleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.UpdateModule.UpdateModuleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.ArchiveModule.ArchiveModuleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.CreateLesson.CreateLessonEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.UpdateLesson.UpdateLessonEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.ArchiveLesson.ArchiveLessonEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.PublishModule.PublishModuleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Academic.PublishLesson.PublishLessonEndpoint.Map(endpoints);
    }

    internal static async Task<IResult> ChangeCourseStatusAsync(
        Guid courseId, CourseStatus target, HttpContext context, IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        if (!AcademicAuthorization.CanManageCourse(actor!, course))
        {
            return Results.Forbid();
        }

        if (course.Status == CourseStatus.Archived)
        {
            return Results.Conflict(new { error = "The course is already archived." });
        }

        course.Status = target;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(course);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(CourseResponse.From(course));
    }


    internal static async Task<IResult> ChangeContentStatusAsync(Guid courseId, Guid moduleId,
        Guid? lessonId, AcademicContentStatus target, HttpContext context, IDocumentSession session,
        IMessageBus messageBus, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out _))
        {
            return Results.Forbid();
        }

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        var module = await session.LoadAsync<CourseModule>(moduleId, cancellationToken);
        if (course is null || module is null || module.CourseId != courseId)
        {
            return Results.NotFound();
        }

        if (!AuthenticatedActor.TryCreate(context.User, out var actor) ||
            !AcademicAuthorization.CanManageCourse(actor!, course))
        {
            return Results.Forbid();
        }

        if (course.Status == CourseStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived course cannot be changed." });
        }

        if (lessonId is null)
        {
            if (module.Status == AcademicContentStatus.Archived)
            {
                return Results.Conflict(new { error = "An archived module cannot change status." });
            }

            module.Status = target;
            if (target == AcademicContentStatus.Archived)
            {
                var lessons = await session.Query<Lesson>()
                    .Where(lesson => lesson.CourseModuleId == moduleId &&
                        lesson.Status != AcademicContentStatus.Archived)
                    .ToListAsync(cancellationToken);
                foreach (var childLesson in lessons)
                {
                    childLesson.Status = AcademicContentStatus.Archived;
                    session.Update(childLesson);
                }
            }
            session.Update(module);
            await session.SaveChangesAsync(cancellationToken);
            await PublishCourseContentReportingRefreshAsync(messageBus, loggerFactory, course);
            return Results.Ok(ModuleResponse.From(module));
        }

        var lesson = await session.LoadAsync<Lesson>(lessonId.Value, cancellationToken);
        if (lesson is null || lesson.CourseId != courseId || lesson.CourseModuleId != moduleId)
        {
            return Results.NotFound();
        }

        if (lesson.Status == AcademicContentStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived lesson cannot change status." });
        }

        if (target == AcademicContentStatus.Published && module.Status != AcademicContentStatus.Published)
        {
            return Results.Conflict(new { error = "Publish the parent module before publishing this lesson." });
        }

        lesson.Status = target;
        session.Update(lesson);
        await session.SaveChangesAsync(cancellationToken);
        await PublishCourseContentReportingRefreshAsync(messageBus, loggerFactory, course);
        return Results.Ok(LessonResponse.From(lesson));
    }


    internal static async Task PublishCourseContentReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Course course)
    {
        try
        {
            await messageBus.PublishAsync(new CourseContentChangedForReporting(course.OrganizationId,
                course.Id));
        }
        catch (Exception exception)
        {
            // Course content remains durable even if the asynchronous reporting
            // queue is temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Academics.CourseContent").LogWarning(exception,
                "Unable to enqueue course-content reporting refresh for {CourseId}.", course.Id);
        }
    }

}
