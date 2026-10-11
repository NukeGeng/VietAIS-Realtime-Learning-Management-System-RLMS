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

namespace LearningPlatform.Academics.Features.Class.ListMySchedules;

internal static class ListMySchedulesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/schedules/me", ListMySchedulesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadScheduling);
    }


    private static async Task<IResult> ListMySchedulesAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool? includeHistory,
        HttpContext context,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        IQueryable<ClassSchedule> query = session.Query<ClassSchedule>()
            .Where(schedule => schedule.OrganizationId == actor!.OrganizationId);
        if (includeHistory == false)
        {
            query = query.Where(schedule => schedule.Status == ScheduleStatus.Scheduled ||
                schedule.Status == ScheduleStatus.Rescheduled);
        }
        if (!actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal))
        {
            IReadOnlyList<Guid> classIds;
            if (actor.Permissions.Contains(PermissionCatalog.ManageScheduling,
                    StringComparer.Ordinal))
            {
                var assigned = await session.Query<ClassTeacherAssignment>()
                    .Where(assignment => assignment.OrganizationId == actor.OrganizationId &&
                        assignment.TeacherId == actor.UserId &&
                        assignment.Status == TeacherAssignmentStatus.Active)
                    .Select(assignment => assignment.LearningClassId)
                    .ToListAsync(cancellationToken);
                var created = await session.Query<LearningClass>()
                    .Where(learningClass => learningClass.OrganizationId == actor.OrganizationId &&
                        learningClass.CreatedBy == actor.UserId)
                    .Select(learningClass => learningClass.Id)
                    .ToListAsync(cancellationToken);
                classIds = assigned.Concat(created).Distinct().ToList();
            }
            else
            {
                classIds = await session.Query<Enrollment>()
                    .Where(enrollment => enrollment.OrganizationId == actor.OrganizationId &&
                        enrollment.StudentId == actor.UserId &&
                        enrollment.Status == EnrollmentStatus.Active)
                    .Select(enrollment => enrollment.LearningClassId)
                    .ToListAsync(cancellationToken);
            }

            if (classIds.Count == 0)
            {
                return Results.Ok(Array.Empty<ScheduleResponse>());
            }

            query = query.Where(schedule => classIds.Contains(schedule.LearningClassId));
        }

        if (from is { } start)
        {
            query = query.Where(schedule => schedule.EndsAt >= start);
        }

        if (to is { } end)
        {
            query = query.Where(schedule => schedule.StartsAt <= end);
        }

        var schedules = await query.OrderBy(schedule => schedule.StartsAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Results.Ok(schedules.Select(ScheduleResponse.From));
    }

}
