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

namespace LearningPlatform.Academics.Features.Class.Reschedule;

internal static class RescheduleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules/{scheduleId:guid}/reschedule",
                RescheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
    }


    private static async Task<IResult> RescheduleAsync(
        Guid classId,
        Guid scheduleId,
        RescheduleRequest request,
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

        if (schedule.Status == ScheduleStatus.Cancelled || schedule.Status == ScheduleStatus.Completed)
        {
            return Results.Conflict(new { error = "This schedule cannot be rescheduled." });
        }

        try
        {
            var times = AcademicRules.RequireTimeRange(request.StartsAt, request.EndsAt);
            var duplicate = await session.Query<ClassSchedule>().AnyAsync(value =>
                value.Id != scheduleId && value.OrganizationId == learningClass.OrganizationId &&
                value.LearningClassId == classId && value.TeacherId == schedule.TeacherId &&
                value.StartsAt == times.Start && value.EndsAt == times.End &&
                (value.Status == ScheduleStatus.Scheduled ||
                 value.Status == ScheduleStatus.Rescheduled),
                cancellationToken);
            if (duplicate)
            {
                return Results.Conflict(new { error = "Buổi học này đã trùng với một lịch đang hoạt động." });
            }

            schedule.StartsAt = times.Start;
            schedule.EndsAt = times.End;
            schedule.Recurrence = AcademicRules.RequireRecurrence(request.Recurrence);
            schedule.Status = ScheduleStatus.Rescheduled;
            schedule.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(schedule);
            await session.SaveChangesAsync(cancellationToken);
            await PublishScheduleNotificationAsync(messageBus, loggerFactory, learningClass.OrganizationId,
                classId, schedule, "schedule-rescheduled", "Lịch học đã thay đổi",
                "Buổi học đã được đổi lịch.");
            return Results.Ok(ScheduleResponse.From(schedule));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
