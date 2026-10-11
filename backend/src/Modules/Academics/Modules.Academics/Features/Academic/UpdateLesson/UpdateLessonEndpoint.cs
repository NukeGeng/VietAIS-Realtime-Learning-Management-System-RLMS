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

namespace LearningPlatform.Academics.Features.Academic.UpdateLesson;

internal static class UpdateLessonEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/lessons/{lessonId:guid}",
                UpdateLessonAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> UpdateLessonAsync(
        Guid courseId,
        Guid moduleId,
        Guid lessonId,
        UpdateLessonRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        var module = await session.LoadAsync<CourseModule>(moduleId, cancellationToken);
        var lesson = await session.LoadAsync<Lesson>(lessonId, cancellationToken);
        if (course is null || module is null || lesson is null || module.CourseId != courseId ||
            lesson.CourseId != courseId || lesson.CourseModuleId != moduleId)
        {
            return Results.NotFound();
        }

        if (!AuthenticatedActor.TryCreate(context.User, out var actor) ||
            !AcademicAuthorization.CanManageCourse(actor!, course))
        {
            return Results.Forbid();
        }

        if (course.Status == CourseStatus.Archived || module.Status == AcademicContentStatus.Archived ||
            lesson.Status == AcademicContentStatus.Archived)
        {
            return Results.Conflict(new { error = "Archived course content cannot be changed." });
        }

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var summary = AcademicRules.RequireContentSummary(request.ContentSummary);
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<Lesson>()
                .AnyAsync(value => value.CourseModuleId == moduleId && value.Id != lessonId &&
                    value.Order == order,
                    cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A lesson with that order already exists." });
            }

            lesson.Title = title;
            lesson.ContentSummary = summary;
            lesson.Order = order;
            session.Update(lesson);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(LessonResponse.From(lesson));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
