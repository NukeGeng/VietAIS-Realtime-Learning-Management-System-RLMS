using JasperFx;
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
        endpoints.MapGet("/api/academics/classes", ListClassesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
        endpoints.MapGet("/api/academics/classes/{classId:guid}", GetClassAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
        endpoints.MapPost("/api/academics/classes", CreateClassAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/teachers", AssignTeacherAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
        endpoints.MapDelete("/api/academics/classes/{classId:guid}/teachers/{teacherId:guid}",
                RemoveTeacherAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
        endpoints.MapGet("/api/academics/classes/{classId:guid}/enrollments", ListEnrollmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadEnrollments);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/enrollments", EnrollAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageEnrollments);
        endpoints.MapDelete("/api/academics/classes/{classId:guid}/enrollments/{studentId:guid}",
                DropEnrollmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageEnrollments);
        endpoints.MapGet("/api/academics/classes/{classId:guid}/schedules", ListSchedulesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadScheduling);
        endpoints.MapGet("/api/academics/schedules/me", ListMySchedulesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadScheduling);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules", CreateScheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules/{scheduleId:guid}/reschedule",
                RescheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/schedules/{scheduleId:guid}/cancel",
                CancelScheduleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageScheduling);
        endpoints.MapPost("/api/academics/classes/{classId:guid}/status", ChangeClassStatusAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }

    private static async Task<IResult> ListClassesAsync(
        HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        IQueryable<LearningClass> query = session.Query<LearningClass>();
        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            // Organization administrators can see every class in their tenant.
        }
        else if (actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal))
        {
            var assignedClassIds = await session.Query<ClassTeacherAssignment>()
                .Where(assignment => assignment.TeacherId == actor.UserId &&
                    assignment.Status == TeacherAssignmentStatus.Active)
                .Select(assignment => assignment.LearningClassId)
                .ToListAsync(cancellationToken);
            var createdClassIds = await session.Query<LearningClass>()
                .Where(learningClass => learningClass.CreatedBy == actor.UserId)
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
                .Where(enrollment => enrollment.StudentId == actor.UserId &&
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
        return Results.Ok(classes.Select(ClassResponse.From));
    }

    private static async Task<IResult> GetClassAsync(
        Guid classId, HttpContext context, IQuerySession session, CancellationToken cancellationToken)
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

        var teachers = await session.Query<ClassTeacherAssignment>()
            .Where(assignment => assignment.LearningClassId == classId &&
                assignment.Status == TeacherAssignmentStatus.Active)
            .OrderBy(assignment => assignment.AssignedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(ClassDetailResponse.From(learningClass, teachers));
    }

    private static async Task<IResult> CreateClassAsync(
        CreateClassRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var course = await session.LoadAsync<Course>(request.CourseId, cancellationToken);
            if (course is null)
            {
                return Results.BadRequest(new { error = "The course does not exist." });
            }

            if (course.Status != CourseStatus.Published)
            {
                return Results.Conflict(new { error = "A class can only use a published course." });
            }

            var code = AcademicRules.NormalizeCourseCode(request.Code);
            var name = AcademicRules.RequireTitle(request.Name);
            var dates = AcademicRules.RequireDateRange(request.StartDate, request.EndDate);
            var now = DateTimeOffset.UtcNow;
            var learningClass = new LearningClass
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                CourseId = request.CourseId,
                Code = code,
                Name = name,
                CreatedBy = actor.UserId,
                Status = LearningClassStatus.Preparing,
                StartDate = dates.Start,
                EndDate = dates.End,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(learningClass);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/classes/{learningClass.Id:D}",
                ClassResponse.From(learningClass));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A class with that code already exists." });
        }
    }

    private static async Task<IResult> AssignTeacherAsync(
        Guid classId,
        AssignTeacherRequest request,
        HttpContext context,
        IDocumentSession session,
        IIdentityDirectory identity,
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
            return Results.Conflict(new { error = "A completed or archived class cannot change its teachers." });
        }

        var teacher = await identity.FindActiveMemberAsync(actor!.OrganizationId, request.TeacherId,
            cancellationToken);
        if (teacher is null || !await identity.HasPermissionAsync(actor.OrganizationId, request.TeacherId,
                PermissionCatalog.ManageClasses, cancellationToken))
        {
            return Results.BadRequest(new { error = "The assigned user is not an active teacher." });
        }

        var assignmentId = AcademicConstants.TeacherAssignmentId(classId, request.TeacherId);
        var assignment = await session.LoadAsync<ClassTeacherAssignment>(assignmentId, cancellationToken);
        if (assignment is not null && assignment.Status == TeacherAssignmentStatus.Active)
        {
            return Results.Conflict(new { error = "The teacher is already assigned to this class." });
        }

        var now = DateTimeOffset.UtcNow;
        if (assignment is null)
        {
            assignment = new ClassTeacherAssignment
            {
                Id = assignmentId,
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                TeacherId = request.TeacherId,
                Status = TeacherAssignmentStatus.Active,
                AssignedAt = now
            };
            session.Insert(assignment);
        }
        else
        {
            assignment.Status = TeacherAssignmentStatus.Active;
            assignment.AssignedAt = now;
            assignment.RemovedAt = null;
            session.Update(assignment);
        }

        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/academics/classes/{classId:D}/teachers/{request.TeacherId:D}",
            TeacherAssignmentResponse.From(assignment));
    }

    private static async Task<IResult> RemoveTeacherAsync(
        Guid classId,
        Guid teacherId,
        HttpContext context,
        IDocumentSession session,
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
            return Results.Conflict(new { error = "A completed or archived class cannot change its teachers." });
        }

        var assignment = await session.LoadAsync<ClassTeacherAssignment>(
            AcademicConstants.TeacherAssignmentId(classId, teacherId), cancellationToken);
        if (assignment is null || assignment.Status == TeacherAssignmentStatus.Removed)
        {
            return Results.NotFound();
        }

        assignment.Status = TeacherAssignmentStatus.Removed;
        assignment.RemovedAt = DateTimeOffset.UtcNow;
        session.Update(assignment);
        await session.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListEnrollmentsAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var canManageClass = await CanManageClassAsync(context, session, learningClass,
            cancellationToken);
        if (!canManageClass && !await session.Query<Enrollment>().AnyAsync(enrollment =>
                enrollment.LearningClassId == classId && enrollment.StudentId == actor!.UserId &&
                enrollment.Status == EnrollmentStatus.Active, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<Enrollment>().Where(enrollment => enrollment.LearningClassId == classId);
        if (!canManageClass)
        {
            query = query.Where(enrollment => enrollment.StudentId == actor!.UserId);
        }

        var enrollments = await query.OrderBy(enrollment => enrollment.EnrolledAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(enrollments.Select(EnrollmentResponse.From));
    }

    private static async Task<IResult> EnrollAsync(
        Guid classId,
        EnrollStudentRequest request,
        HttpContext context,
        IDocumentSession session,
        IIdentityDirectory identity,
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
            return Results.Conflict(new { error = "A completed or archived class cannot change enrollment." });
        }

        if (await identity.FindActiveMemberAsync(actor!.OrganizationId, request.StudentId,
                cancellationToken) is null ||
            !await identity.HasPermissionAsync(actor.OrganizationId, request.StudentId,
                PermissionCatalog.ReadClasses, cancellationToken))
        {
            return Results.BadRequest(new { error = "The student is not an active class member." });
        }

        var enrollmentId = AcademicConstants.EnrollmentId(classId, request.StudentId);
        var enrollment = await session.LoadAsync<Enrollment>(enrollmentId, cancellationToken);
        if (enrollment is not null && enrollment.Status == EnrollmentStatus.Active)
        {
            return Results.Conflict(new { error = "The student is already enrolled in this class." });
        }

        var now = DateTimeOffset.UtcNow;
        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                Id = enrollmentId,
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                StudentId = request.StudentId,
                Status = EnrollmentStatus.Active,
                EnrolledAt = now,
                UpdatedAt = now
            };
            session.Insert(enrollment);
        }
        else if (enrollment.Status == EnrollmentStatus.Dropped)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.EnrolledAt = now;
            enrollment.UpdatedAt = now;
            session.Update(enrollment);
        }
        else
        {
            return Results.Conflict(new { error = "A completed enrollment cannot be reopened." });
        }

        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/academics/classes/{classId:D}/enrollments/{request.StudentId:D}",
            EnrollmentResponse.From(enrollment));
    }

    private static async Task<IResult> DropEnrollmentAsync(
        Guid classId,
        Guid studentId,
        HttpContext context,
        IDocumentSession session,
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
            return Results.Conflict(new { error = "A completed or archived class cannot change enrollment." });
        }

        var enrollment = await session.LoadAsync<Enrollment>(
            AcademicConstants.EnrollmentId(classId, studentId), cancellationToken);
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
        {
            return Results.NotFound();
        }

        enrollment.Status = EnrollmentStatus.Dropped;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(enrollment);
        await session.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListSchedulesAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
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

        var schedules = await session.Query<ClassSchedule>()
            .Where(schedule => schedule.LearningClassId == classId)
            .OrderBy(schedule => schedule.StartsAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(schedules.Select(ScheduleResponse.From));
    }

    private static async Task<IResult> ListMySchedulesAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        HttpContext context,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        IQueryable<ClassSchedule> query = session.Query<ClassSchedule>();
        if (!actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal))
        {
            IReadOnlyList<Guid> classIds;
            if (actor.Permissions.Contains(PermissionCatalog.ManageScheduling,
                    StringComparer.Ordinal))
            {
                var assigned = await session.Query<ClassTeacherAssignment>()
                    .Where(assignment => assignment.TeacherId == actor.UserId &&
                        assignment.Status == TeacherAssignmentStatus.Active)
                    .Select(assignment => assignment.LearningClassId)
                    .ToListAsync(cancellationToken);
                var created = await session.Query<LearningClass>()
                    .Where(learningClass => learningClass.CreatedBy == actor.UserId)
                    .Select(learningClass => learningClass.Id)
                    .ToListAsync(cancellationToken);
                classIds = assigned.Concat(created).Distinct().ToList();
            }
            else
            {
                classIds = await session.Query<Enrollment>()
                    .Where(enrollment => enrollment.StudentId == actor.UserId &&
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

    private static async Task<IResult> ChangeClassStatusAsync(
        Guid classId,
        ChangeClassStatusRequest request,
        HttpContext context,
        IDocumentSession session,
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

        if (!CanChangeStatus(learningClass.Status, request.Status))
        {
            return Results.Conflict(new { error = "The class cannot make that state transition." });
        }

        learningClass.Status = request.Status;
        learningClass.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(learningClass);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(ClassResponse.From(learningClass));
    }

    private static bool CanChangeStatus(LearningClassStatus current, LearningClassStatus target) =>
        (current, target) switch
        {
            (LearningClassStatus.Preparing, LearningClassStatus.Active or LearningClassStatus.Archived) => true,
            (LearningClassStatus.Active, LearningClassStatus.Paused or LearningClassStatus.Completed) => true,
            (LearningClassStatus.Paused, LearningClassStatus.Active or LearningClassStatus.Completed) => true,
            (LearningClassStatus.Completed, LearningClassStatus.Archived) => true,
            _ => false
        };

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
                classId, schedule, "schedule-created", "New class session", "A new class session has been scheduled.");
            return Results.Created($"/api/academics/classes/{classId:D}/schedules/{schedule.Id:D}",
                ScheduleResponse.From(schedule));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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
            schedule.StartsAt = times.Start;
            schedule.EndsAt = times.End;
            schedule.Recurrence = AcademicRules.RequireRecurrence(request.Recurrence);
            schedule.Status = ScheduleStatus.Rescheduled;
            schedule.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(schedule);
            await session.SaveChangesAsync(cancellationToken);
            await PublishScheduleNotificationAsync(messageBus, loggerFactory, learningClass.OrganizationId,
                classId, schedule, "schedule-rescheduled", "Class session changed",
                "A class session has been rescheduled.");
            return Results.Ok(ScheduleResponse.From(schedule));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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
            classId, schedule, "schedule-cancelled", "Class session cancelled",
            "A class session has been cancelled.");
        return Results.NoContent();
    }

    private static async Task PublishScheduleNotificationAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Guid organizationId, Guid classId, ClassSchedule schedule,
        string type, string title, string body)
    {
        try
        {
            await messageBus.PublishAsync(new ClassNotificationRequested(Guid.NewGuid(), organizationId,
                classId, type, title,
                $"{body} Session: {schedule.StartsAt:u} - {schedule.EndsAt:u}.", schedule.Id,
                schedule.UpdatedAt));
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LearningPlatform.Academics.Scheduling").LogWarning(exception,
                "Unable to enqueue schedule notification for {ScheduleId}.", schedule.Id);
        }
    }

    private static async Task<bool> CanReadClassAsync(
        HttpContext context, IQuerySession session, LearningClass learningClass,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            learningClass.CreatedBy == actor.UserId)
        {
            return true;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal) &&
            await session.Query<ClassTeacherAssignment>().AnyAsync(assignment =>
                assignment.LearningClassId == learningClass.Id && assignment.TeacherId == actor.UserId &&
                assignment.Status == TeacherAssignmentStatus.Active, cancellationToken))
        {
            return true;
        }

        return await session.Query<Enrollment>().AnyAsync(enrollment =>
            enrollment.LearningClassId == learningClass.Id &&
            enrollment.StudentId == actor.UserId &&
            enrollment.Status == EnrollmentStatus.Active, cancellationToken);
    }

    private static async Task<bool> CanManageClassAsync(
        HttpContext context, IQuerySession session, LearningClass learningClass,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return false;
        }

        if (actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            learningClass.CreatedBy == actor.UserId)
        {
            return true;
        }

        return actor.Permissions.Contains(PermissionCatalog.ManageClasses, StringComparer.Ordinal) &&
            await session.Query<ClassTeacherAssignment>().AnyAsync(assignment =>
                assignment.LearningClassId == learningClass.Id && assignment.TeacherId == actor.UserId &&
                assignment.Status == TeacherAssignmentStatus.Active, cancellationToken);
    }
}

public sealed record CreateClassRequest(Guid CourseId, string Code, string Name,
    DateOnly StartDate, DateOnly EndDate);
public sealed record AssignTeacherRequest(Guid TeacherId);
public sealed record EnrollStudentRequest(Guid StudentId);
public sealed record CreateScheduleRequest(Guid TeacherId, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, string? Recurrence);
public sealed record RescheduleRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? Recurrence);
public sealed record ChangeClassStatusRequest(LearningClassStatus Status);

public sealed record ClassResponse(Guid Id, Guid CourseId, string Code, string Name,
    LearningClassStatus Status, DateOnly StartDate, DateOnly EndDate)
{
    public static ClassResponse From(LearningClass learningClass) => new(learningClass.Id,
        learningClass.CourseId, learningClass.Code, learningClass.Name, learningClass.Status,
        learningClass.StartDate, learningClass.EndDate);
}

public sealed record ClassDetailResponse(ClassResponse Class, IReadOnlyList<Guid> TeacherIds)
{
    public static ClassDetailResponse From(LearningClass learningClass,
        IEnumerable<ClassTeacherAssignment> teachers) => new(ClassResponse.From(learningClass),
        teachers.Select(teacher => teacher.TeacherId).ToArray());
}

public sealed record TeacherAssignmentResponse(Guid Id, Guid LearningClassId, Guid TeacherId,
    TeacherAssignmentStatus Status, DateTimeOffset AssignedAt)
{
    public static TeacherAssignmentResponse From(ClassTeacherAssignment assignment) => new(
        assignment.Id, assignment.LearningClassId, assignment.TeacherId, assignment.Status,
        assignment.AssignedAt);
}

public sealed record EnrollmentResponse(Guid Id, Guid LearningClassId, Guid StudentId,
    EnrollmentStatus Status, DateTimeOffset EnrolledAt)
{
    public static EnrollmentResponse From(Enrollment enrollment) => new(enrollment.Id,
        enrollment.LearningClassId, enrollment.StudentId, enrollment.Status, enrollment.EnrolledAt);
}

public sealed record ScheduleResponse(Guid Id, Guid LearningClassId, Guid TeacherId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Recurrence, ScheduleStatus Status)
{
    public static ScheduleResponse From(ClassSchedule schedule) => new(schedule.Id,
        schedule.LearningClassId, schedule.TeacherId, schedule.StartsAt, schedule.EndsAt,
        schedule.Recurrence, schedule.Status);
}
