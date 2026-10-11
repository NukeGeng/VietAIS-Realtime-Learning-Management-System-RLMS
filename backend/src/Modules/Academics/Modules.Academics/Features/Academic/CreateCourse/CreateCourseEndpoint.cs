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

namespace LearningPlatform.Academics.Features.Academic.CreateCourse;

internal static class CreateCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses", CreateCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> CreateCourseAsync(
        CreateCourseRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var code = AcademicRules.NormalizeCourseCode(request.Code);
            var title = AcademicRules.RequireTitle(request.Title);
            var description = AcademicRules.RequireDescription(request.Description);
            var now = DateTimeOffset.UtcNow;
            var course = new Course
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                Code = code,
                Title = title,
                Description = description,
                Status = CourseStatus.Draft,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(course);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{course.Id:D}", CourseResponse.From(course));
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
