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

namespace LearningPlatform.Academics.Features.Academic.CreateModule;

internal static class CreateModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules", CreateModuleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> CreateModuleAsync(
        Guid courseId,
        CreateModuleRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
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
            return Results.Conflict(new { error = "Archived course content cannot be changed." });
        }

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<CourseModule>()
                .AnyAsync(module => module.CourseId == courseId && module.Order == order, cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A module with that order already exists." });
            }

            var module = new CourseModule
            {
                Id = Guid.NewGuid(),
                OrganizationId = course.OrganizationId,
                CourseId = courseId,
                Title = title,
                Order = order,
                Status = AcademicContentStatus.Draft
            };
            session.Insert(module);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{courseId:D}/modules/{module.Id:D}",
                ModuleResponse.From(module));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
