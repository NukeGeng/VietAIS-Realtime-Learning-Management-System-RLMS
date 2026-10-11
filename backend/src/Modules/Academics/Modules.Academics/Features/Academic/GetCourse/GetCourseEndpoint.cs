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

namespace LearningPlatform.Academics.Features.Academic.GetCourse;

internal static class GetCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/courses/{courseId:guid}", GetCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadCourses);
    }


    private static async Task<IResult> GetCourseAsync(
        Guid courseId, HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null || course.OrganizationId != actor!.OrganizationId)
        {
            return Results.NotFound();
        }

        var canManage = AcademicAuthorization.CanManageCourse(actor!, course);
        var canReadPublished = actor!.Permissions.Contains(PermissionCatalog.ManageCourses,
            StringComparer.Ordinal) && course.Status == CourseStatus.Published;
        var canRead = canManage || canReadPublished;
        if (!canRead)
        {
            var classIds = await session.Query<LearningClass>()
                .Where(learningClass => learningClass.CourseId == courseId)
                .Select(learningClass => learningClass.Id)
                .ToListAsync(cancellationToken);
            canRead = course.Status == CourseStatus.Published && classIds.Count > 0 &&
                await session.Query<Enrollment>().AnyAsync(enrollment =>
                    classIds.Contains(enrollment.LearningClassId) &&
                    enrollment.StudentId == actor.UserId &&
                    enrollment.Status == EnrollmentStatus.Active, cancellationToken);
        }

        if (!canRead)
        {
            return Results.Forbid();
        }

        var modules = await session.Query<CourseModule>()
            .Where(module => module.CourseId == courseId)
            .OrderBy(module => module.Order)
            .ToListAsync(cancellationToken);
        var lessons = await session.Query<Lesson>()
            .Where(lesson => lesson.CourseId == courseId)
            .OrderBy(lesson => lesson.Order)
            .ToListAsync(cancellationToken);
        if (!canManage)
        {
            modules = modules.Where(module => module.Status == AcademicContentStatus.Published).ToList();
            lessons = lessons.Where(lesson => lesson.Status == AcademicContentStatus.Published).ToList();
        }
        return Results.Ok(CourseDetailResponse.From(course, modules, lessons));
    }

}
