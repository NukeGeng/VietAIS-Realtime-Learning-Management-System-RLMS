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

namespace LearningPlatform.Academics.Features.Class.CancelSchedule;

internal static class CancelScheduleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules/{scheduleId:guid}/cancel",
                CancelScheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
    }


    private static async Task<IResult> CancelScheduleAsync(
        Guid classId,
        Guid scheduleId,
        HttpContext context,
        IDocumentSession session,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(context, session, learningClass, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change its schedule." });
        }

        var schedule = await session.LoadAsync<ClassSchedule>(scheduleId, cancellationToken);
        if (schedule is null || schedule.LearningClassId != classId)
        {
            return Results.NotFound();
        }

        if (schedule.Status == ScheduleStatus.Cancelled)
        {
            return Results.NoContent();
        }

        if (schedule.Status == ScheduleStatus.Completed)
        {
            return Results.Conflict(new { error = "A completed schedule cannot be cancelled." });
        }

        schedule.Status = ScheduleStatus.Cancelled;
        schedule.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(schedule);
        await session.SaveChangesAsync(cancellationToken);
        await PublishScheduleNotificationAsync(messageBus, loggerFactory, learningClass.OrganizationId,
            classId, schedule, "schedule-cancelled", "Lịch học đã hủy",
            "Buổi học đã được tạm ngưng.");
        return Results.NoContent();
    }

}
