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
using static LearningPlatform.Academics.Api.ClassEndpoints;

namespace LearningPlatform.Academics.Features.Class.ListClasses;

internal static class ListClassesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/classes", ListClassesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
    }


    private static async Task<IResult> ListClassesAsync(
        HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        IQueryable<LearningClass> query = session.Query<LearningClass>()
            .Where(learningClass => learningClass.OrganizationId == actor!.OrganizationId);
        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            // Organization administrators can see every class in their tenant.
        }
        else if (actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal))
        {
            var assignedClassIds = await session.Query<ClassTeacherAssignment>()
                .Where(assignment => assignment.OrganizationId == actor.OrganizationId &&
                    assignment.TeacherId == actor.UserId &&
                    assignment.Status == TeacherAssignmentStatus.Active)
                .Select(assignment => assignment.LearningClassId)
                .ToListAsync(cancellationToken);
            var createdClassIds = await session.Query<LearningClass>()
                .Where(learningClass => learningClass.OrganizationId == actor.OrganizationId &&
                    learningClass.CreatedBy == actor.UserId)
                .Select(learningClass => learningClass.Id)
                .ToListAsync(cancellationToken);
            var classIds = assignedClassIds.Concat(createdClassIds).Distinct().ToList();
            if (classIds.Count == 0)
            {
                return Results.Ok(Array.Empty<ClassResponse>());
            }

            query = query.Where(learningClass => classIds.Contains(learningClass.Id));
        }
        else
        {
            var classIds = await session.Query<Enrollment>()
                .Where(enrollment => enrollment.OrganizationId == actor.OrganizationId &&
                    enrollment.StudentId == actor.UserId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.LearningClassId)
                .ToListAsync(cancellationToken);
            if (classIds.Count == 0)
            {
                return Results.Ok(Array.Empty<ClassResponse>());
            }

            query = query.Where(learningClass => classIds.Contains(learningClass.Id));
        }

        var classes = await query.OrderBy(learningClass => learningClass.Name)
            .ToListAsync(cancellationToken);
        var courseIds = classes.Select(item => item.CourseId).Distinct().ToArray();
        var courses = await session.Query<Course>()
            .Where(item => item.OrganizationId == actor.OrganizationId && courseIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var visibleIds = classes.Select(item => item.Id).ToArray();
        var enrollments = await session.Query<Enrollment>()
            .Where(item => item.OrganizationId == actor.OrganizationId && visibleIds.Contains(item.LearningClassId) && item.Status == EnrollmentStatus.Active)
            .Select(item => item.LearningClassId).ToListAsync(cancellationToken);
        var counts = enrollments.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
        var titles = courses.ToDictionary(item => item.Id, item => item.Title);
        return Results.Ok(classes.Select(item => ClassResponse.From(item) with
        {
            CourseName = titles.GetValueOrDefault(item.CourseId),
            StudentCount = counts.GetValueOrDefault(item.Id)
        }));
    }

}
