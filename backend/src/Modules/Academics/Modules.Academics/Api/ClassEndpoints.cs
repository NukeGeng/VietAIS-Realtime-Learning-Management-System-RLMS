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

namespace LearningPlatform.Academics.Api;

public static class ClassEndpoints
{
    public static void MapClassEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Academics.Features.Class.ListClasses.ListClassesEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.GetClass.GetClassEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.CreateClass.CreateClassEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.AssignTeacher.AssignTeacherEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.RemoveTeacher.RemoveTeacherEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.ListEnrollments.ListEnrollmentsEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.Enroll.EnrollEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.DropEnrollment.DropEnrollmentEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.ListSchedules.ListSchedulesEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.ListMySchedules.ListMySchedulesEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.CreateSchedule.CreateScheduleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.Reschedule.RescheduleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.CancelSchedule.CancelScheduleEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.ListClassCurriculums.ListClassCurriculumsEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.AssignCurriculum.AssignCurriculumEndpoint.Map(endpoints);
        LearningPlatform.Academics.Features.Class.ChangeClassStatus.ChangeClassStatusEndpoint.Map(endpoints);
    }

    internal static async Task PublishEnrollmentReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Enrollment enrollment)
    {
        try
        {
            await messageBus.PublishAsync(new EnrollmentChangedForReporting(enrollment.OrganizationId,
                enrollment.LearningClassId, enrollment.StudentId, enrollment.Status));
        }
        catch (Exception exception)
        {
            // Enrollment is durable even if the asynchronous reporting queue is
            // temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Academics.Enrollment").LogWarning(exception,
                "Unable to enqueue enrollment reporting refresh for {ClassId}/{StudentId}.",
                enrollment.LearningClassId, enrollment.StudentId);
        }
    }


    internal static bool CanChangeStatus(LearningClassStatus current, LearningClassStatus target) =>
        (current, target) switch
        {
            (LearningClassStatus.Preparing, LearningClassStatus.Active or LearningClassStatus.Archived) => true,
            (LearningClassStatus.Active, LearningClassStatus.Paused or LearningClassStatus.Completed) => true,
            (LearningClassStatus.Paused, LearningClassStatus.Active or LearningClassStatus.Completed) => true,
            (LearningClassStatus.Completed, LearningClassStatus.Archived) => true,
            _ => false
        };


    internal static async Task PublishScheduleNotificationAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Guid organizationId, Guid classId, ClassSchedule schedule,
        string type, string title, string body)
    {
        try
        {
            var occurredAt = schedule.UpdatedAt;
            await messageBus.PublishAsync(new ClassNotificationRequested(
                NotificationEventIds.Transition(schedule.Id, type, occurredAt), organizationId,
                classId, type, title,
                $"{body} Thời gian: {schedule.StartsAt:dd/MM/yyyy HH:mm} - {schedule.EndsAt:HH:mm}.", schedule.Id,
                occurredAt));
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LearningPlatform.Academics.Scheduling").LogWarning(exception,
                "Unable to enqueue schedule notification for {ScheduleId}.", schedule.Id);
        }
    }


    internal static async Task<bool> CanReadClassAsync(
        HttpContext context, IQuerySession session, LearningClass learningClass,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        if (learningClass.OrganizationId != actor!.OrganizationId)
        {
            return false;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            learningClass.CreatedBy == actor.UserId)
        {
            return true;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal) &&
            await session.Query<ClassTeacherAssignment>().AnyAsync(assignment =>
                assignment.OrganizationId == actor.OrganizationId &&
                assignment.LearningClassId == learningClass.Id && assignment.TeacherId == actor.UserId &&
                assignment.Status == TeacherAssignmentStatus.Active, cancellationToken))
        {
            return true;
        }

        return await session.Query<Enrollment>().AnyAsync(enrollment =>
            enrollment.OrganizationId == actor.OrganizationId &&
            enrollment.LearningClassId == learningClass.Id &&
            enrollment.StudentId == actor.UserId &&
            enrollment.Status == EnrollmentStatus.Active, cancellationToken);
    }


    internal static async Task<bool> CanManageClassAsync(
        HttpContext context, IQuerySession session, LearningClass learningClass,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        if (learningClass.OrganizationId != actor!.OrganizationId)
        {
            return false;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            learningClass.CreatedBy == actor.UserId)
        {
            return true;
        }

        return actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal) &&
            await session.Query<ClassTeacherAssignment>().AnyAsync(assignment =>
                assignment.OrganizationId == actor.OrganizationId &&
                assignment.LearningClassId == learningClass.Id && assignment.TeacherId == actor.UserId &&
                assignment.Status == TeacherAssignmentStatus.Active, cancellationToken);
    }

}
