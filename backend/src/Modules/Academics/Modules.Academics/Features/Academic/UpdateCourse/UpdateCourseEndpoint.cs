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

namespace LearningPlatform.Academics.Features.Academic.UpdateCourse;

internal static class UpdateCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/academics/courses/{courseId:guid}", UpdateCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> UpdateCourseAsync(
        Guid courseId,
        UpdateCourseRequest request,
        HttpContext context,
        IDocumentSession session,
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
            return Results.Conflict(new { error = "An archived course cannot be changed." });
        }

        try
        {
            course.Code = AcademicRules.NormalizeCourseCode(request.Code);
            course.Title = AcademicRules.RequireTitle(request.Title);
            course.Description = AcademicRules.RequireDescription(request.Description);
            course.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(course);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(CourseResponse.From(course));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A course with that code already exists." });
        }
    }

}
