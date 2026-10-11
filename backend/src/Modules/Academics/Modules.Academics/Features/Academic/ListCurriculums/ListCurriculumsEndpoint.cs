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

namespace LearningPlatform.Academics.Features.Academic.ListCurriculums;

internal static class ListCurriculumsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/courses/{courseId:guid}/curriculums",
                ListCurriculumsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadCourses);
    }


    private static async Task<IResult> ListCurriculumsAsync(
        Guid courseId, HttpContext context, IQuerySession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
            return Results.Forbid();

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null || course.OrganizationId != actor!.OrganizationId)
            return Results.NotFound();

        var canManage = AcademicAuthorization.CanManageCourse(actor, course);
        if (!canManage && course.Status != CourseStatus.Published)
            return Results.Forbid();

        var curriculums = await session.Query<Curriculum>()
            .Where(value => value.OrganizationId == actor.OrganizationId &&
                value.CourseId == courseId &&
                (canManage || value.Status == AcademicContentStatus.Published))
            .OrderBy(value => value.Title)
            .ToListAsync(cancellationToken);
        return Results.Ok(curriculums.Select(CurriculumResponse.From));
    }

}
