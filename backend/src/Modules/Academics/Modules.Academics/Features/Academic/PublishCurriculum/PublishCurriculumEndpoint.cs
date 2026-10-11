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

namespace LearningPlatform.Academics.Features.Academic.PublishCurriculum;

internal static class PublishCurriculumEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/curriculums/{curriculumId:guid}/publish",
                PublishCurriculumAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> PublishCurriculumAsync(
        Guid curriculumId, HttpContext context, IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
            return Results.Forbid();

        var curriculum = await session.LoadAsync<Curriculum>(curriculumId, cancellationToken);
        if (curriculum is null || curriculum.OrganizationId != actor!.OrganizationId)
            return Results.NotFound();
        var course = await session.LoadAsync<Course>(curriculum.CourseId, cancellationToken);
        if (course is null || !AcademicAuthorization.CanManageCourse(actor, course))
            return Results.Forbid();
        if (curriculum.Status == AcademicContentStatus.Archived)
            return Results.Conflict(new { error = "An archived curriculum cannot be published." });

        curriculum.Status = AcademicContentStatus.Published;
        curriculum.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(curriculum);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(CurriculumResponse.From(curriculum));
    }

}
