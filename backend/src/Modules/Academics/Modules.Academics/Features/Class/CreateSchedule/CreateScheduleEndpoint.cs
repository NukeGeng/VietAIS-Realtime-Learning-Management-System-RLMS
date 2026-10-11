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

namespace LearningPlatform.Academics.Features.Class.CreateSchedule;

internal static class CreateScheduleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules", CreateScheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
    }


    private static async Task<IResult> CreateScheduleAsync(
        Guid classId,
        CreateScheduleRequest request,
        HttpContext context,
        IDocumentSession session,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

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
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new schedule." });
        }

        var assignment = await session.LoadAsync<ClassTeacherAssignment>(
            AcademicConstants.TeacherAssignmentId(classId, request.TeacherId), cancellationToken);
        if (assignment is null || assignment.Status != TeacherAssignmentStatus.Active)
        {
            return Results.BadRequest(new { error = "The teacher must be assigned to the class first." });
        }

        try
        {
            var times = AcademicRules.RequireTimeRange(request.StartsAt, request.EndsAt);
            var duplicate = await session.Query<ClassSchedule>().AnyAsync(schedule =>
                schedule.OrganizationId == learningClass.OrganizationId &&
                schedule.LearningClassId == classId && schedule.TeacherId == request.TeacherId &&
                schedule.StartsAt == times.Start && schedule.EndsAt == times.End &&
                (schedule.Status == ScheduleStatus.Scheduled ||
                 schedule.Status == ScheduleStatus.Rescheduled),
                cancellationToken);
            if (duplicate)
            {
                return Results.Conflict(new { error = "Buổi học này đã được lên lịch." });
            }

            var schedule = new ClassSchedule
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                LearningClassId = classId,
                TeacherId = request.TeacherId,
                StartsAt = times.Start,
                EndsAt = times.End,
                Recurrence = AcademicRules.RequireRecurrence(request.Recurrence),
                Status = ScheduleStatus.Scheduled,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            session.Insert(schedule);
            await session.SaveChangesAsync(cancellationToken);
            await PublishScheduleNotificationAsync(messageBus, loggerFactory, actor.OrganizationId,
                classId, schedule, "schedule-created", "Lịch học mới", "Một buổi học mới đã được lên lịch.");
            return Results.Created($"/api/academics/classes/{classId:D}/schedules/{schedule.Id:D}",
                ScheduleResponse.From(schedule));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
