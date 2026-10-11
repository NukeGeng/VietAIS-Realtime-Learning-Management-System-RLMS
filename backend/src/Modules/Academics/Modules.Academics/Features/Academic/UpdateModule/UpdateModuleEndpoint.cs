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

namespace LearningPlatform.Academics.Features.Academic.UpdateModule;

internal static class UpdateModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}",
                UpdateModuleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> UpdateModuleAsync(
        Guid courseId,
        Guid moduleId,
        UpdateModuleRequest request,
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
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<CourseModule>()
                .AnyAsync(value => value.CourseId == courseId && value.Id != moduleId &&
                    value.Order == order,
                    cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A module with that order already exists." });
            }

            module.Title = title;
            module.Order = order;
            session.Update(module);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(ModuleResponse.From(module));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
