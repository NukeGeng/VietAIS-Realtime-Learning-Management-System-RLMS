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

namespace LearningPlatform.Academics.Features.Academic.CreateLesson;

internal static class CreateLessonEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/lessons",
                CreateLessonAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> CreateLessonAsync(
        Guid courseId,
        Guid moduleId,
        CreateLessonRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
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

        if (course.Status == CourseStatus.Archived || module.Status == AcademicContentStatus.Archived)
        {
            return Results.Conflict(new { error = "Archived course content cannot be changed." });
        }

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var summary = AcademicRules.RequireContentSummary(request.ContentSummary);
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<Lesson>()
                .AnyAsync(lesson => lesson.CourseModuleId == moduleId && lesson.Order == order,
                    cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A lesson with that order already exists." });
            }

            var lesson = new Lesson
            {
                Id = Guid.NewGuid(),
                OrganizationId = course.OrganizationId,
                CourseId = courseId,
                CourseModuleId = moduleId,
                Title = title,
                Order = order,
                ContentSummary = summary,
                Status = AcademicContentStatus.Draft
            };
            session.Insert(lesson);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{courseId:D}/modules/{moduleId:D}/lessons/{lesson.Id:D}",
                LessonResponse.From(lesson));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
