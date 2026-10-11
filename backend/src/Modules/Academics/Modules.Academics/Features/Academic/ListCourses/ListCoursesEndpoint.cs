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

namespace LearningPlatform.Academics.Features.Academic.ListCourses;

internal static class ListCoursesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/courses", ListCoursesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadCourses);
    }


    private static async Task<IResult> ListCoursesAsync(
        int? page, int? pageSize, HttpContext context, IQuerySession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var safePageSize = Math.Clamp(pageSize ?? 50, 1, 100);
        // Keep the computed Skip value inside Int32 for the LINQ provider even
        // when a client sends the largest possible page number.
        var maxPage = (int)Math.Min(int.MaxValue,
            (long)int.MaxValue / safePageSize + 1);
        var safePage = Math.Clamp(page ?? 1, 1, maxPage);
        IQueryable<Course> query = session.Query<Course>();
        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal))
        {
            // Organization administrators can see every course in their tenant.
        }
        else if (actor.Permissions.Contains(PermissionCatalog.ManageCourses,
                     StringComparer.Ordinal))
        {
            query = query.Where(course => course.Status == CourseStatus.Published ||
                course.CreatedBy == actor.UserId);
        }
        else
        {
            var classIds = await session.Query<Enrollment>()
                .Where(enrollment => enrollment.StudentId == actor.UserId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.LearningClassId)
                .ToListAsync(cancellationToken);
            if (classIds.Count == 0)
            {
                return Results.Ok(Array.Empty<CourseResponse>());
            }

            var courseIds = await session.Query<LearningClass>()
                .Where(learningClass => classIds.Contains(learningClass.Id))
                .Select(learningClass => learningClass.CourseId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (courseIds.Count == 0)
            {
                return Results.Ok(Array.Empty<CourseResponse>());
            }

            query = query.Where(course => courseIds.Contains(course.Id) &&
                course.Status == CourseStatus.Published);
        }

        var courses = await query.OrderBy(course => course.Title)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return Results.Ok(courses.Select(CourseResponse.From));
    }

}
