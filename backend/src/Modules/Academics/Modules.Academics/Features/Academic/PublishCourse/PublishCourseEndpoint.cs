using LearningPlatform.Academics.Api;
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
using static LearningPlatform.Academics.Api.AcademicEndpoints;

namespace LearningPlatform.Academics.Features.Academic.PublishCourse;

internal static class PublishCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/publish", PublishCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> PublishCourseAsync(
        Guid courseId, HttpContext context, IDocumentSession session,
        IMessageBus messageBus, ILoggerFactory loggerFactory,
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
            return Results.Conflict(new { error = "An archived course cannot be published." });
        }

        course.Status = CourseStatus.Published;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        var modules = await session.Query<CourseModule>()
            .Where(module => module.CourseId == courseId && module.Status != AcademicContentStatus.Archived)
            .ToListAsync(cancellationToken);
        var moduleIds = modules.Select(module => module.Id).ToArray();
        var lessons = await session.Query<Lesson>()
            .Where(lesson => lesson.CourseId == courseId && lesson.Status != AcademicContentStatus.Archived &&
                moduleIds.Contains(lesson.CourseModuleId))
            .ToListAsync(cancellationToken);
        foreach (var module in modules)
        {
            module.Status = AcademicContentStatus.Published;
            session.Update(module);
        }

        foreach (var lesson in lessons)
        {
            lesson.Status = AcademicContentStatus.Published;
            session.Update(lesson);
        }

        session.Update(course);
        await session.SaveChangesAsync(cancellationToken);
        await PublishCourseContentReportingRefreshAsync(messageBus, loggerFactory, course);
        return Results.Ok(CourseResponse.From(course));
    }

}
