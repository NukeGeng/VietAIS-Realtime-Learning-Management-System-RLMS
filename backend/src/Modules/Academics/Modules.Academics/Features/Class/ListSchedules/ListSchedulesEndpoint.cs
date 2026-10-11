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

namespace LearningPlatform.Academics.Features.Class.ListSchedules;

internal static class ListSchedulesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/classes/{classId:guid}/schedules", ListSchedulesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadScheduling);
    }


    private static async Task<IResult> ListSchedulesAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        bool? includeHistory,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanReadClassAsync(context, session, learningClass, cancellationToken))
        {
            return Results.Forbid();
        }

        var schedulesQuery = session.Query<ClassSchedule>()
            .Where(schedule => schedule.OrganizationId == learningClass.OrganizationId &&
                schedule.LearningClassId == classId);
        // Keep the historical default for existing API consumers. The
        // user-facing class/calendar views pass includeHistory=false so they
        // show only the current occurrence lifecycle.
        if (includeHistory == false)
        {
            schedulesQuery = schedulesQuery.Where(schedule =>
                schedule.Status == ScheduleStatus.Scheduled ||
                schedule.Status == ScheduleStatus.Rescheduled);
        }

        var schedules = await schedulesQuery
            .OrderBy(schedule => schedule.StartsAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(schedules.Select(ScheduleResponse.From));
    }

}
