using LearningPlatform.Academics.Domain;
using Marten;

namespace LearningPlatform.Academics.Authorization;

public interface IAcademicDirectory
{
    Task<AcademicClassMember?> FindClassAsync(Guid organizationId, Guid classId,
        CancellationToken cancellationToken = default);

    Task<bool> IsTeacherAssignedAsync(Guid organizationId, Guid classId, Guid teacherId,
        CancellationToken cancellationToken = default);

    Task<bool> IsClassCreatorAsync(Guid organizationId, Guid classId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsStudentEnrolledAsync(Guid organizationId, Guid classId, Guid studentId,
        CancellationToken cancellationToken = default);

    Task<AcademicScheduleReference?> FindScheduleAsync(Guid organizationId, Guid classId, Guid scheduleId,
        CancellationToken cancellationToken = default);
}

public sealed record AcademicClassMember(Guid Id, Guid CourseId, string Code, string Name,
    Guid CreatedBy, LearningClassStatus Status);

public sealed record AcademicScheduleReference(Guid Id, Guid LearningClassId, Guid TeacherId,
    ScheduleStatus Status);

public sealed class AcademicDirectory(IDocumentStore store) : IAcademicDirectory
{
    public async Task<AcademicClassMember?> FindClassAsync(
        Guid organizationId, Guid classId, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession(organizationId.ToString("D"));
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        return learningClass is null
            ? null
            : new AcademicClassMember(learningClass.Id, learningClass.CourseId,
                learningClass.Code, learningClass.Name, learningClass.CreatedBy, learningClass.Status);
    }

    public async Task<bool> IsTeacherAssignedAsync(
        Guid organizationId, Guid classId, Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession(organizationId.ToString("D"));
        return await session.Query<ClassTeacherAssignment>().AnyAsync(assignment =>
            assignment.LearningClassId == classId && assignment.TeacherId == teacherId &&
            assignment.Status == TeacherAssignmentStatus.Active, cancellationToken);
    }

    public async Task<bool> IsClassCreatorAsync(Guid organizationId, Guid classId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        await using var session = store.QuerySession(organizationId.ToString("D"));
        return await session.Query<LearningClass>().AnyAsync(learningClass =>
            learningClass.Id == classId && learningClass.CreatedBy == userId, cancellationToken);
    }

    public async Task<bool> IsStudentEnrolledAsync(
        Guid organizationId, Guid classId, Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession(organizationId.ToString("D"));
        return await session.Query<Enrollment>().AnyAsync(enrollment =>
            enrollment.LearningClassId == classId && enrollment.StudentId == studentId &&
            enrollment.Status == EnrollmentStatus.Active, cancellationToken);
    }

    public async Task<AcademicScheduleReference?> FindScheduleAsync(
        Guid organizationId, Guid classId, Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession(organizationId.ToString("D"));
        var schedule = await session.LoadAsync<ClassSchedule>(scheduleId, cancellationToken);
        return schedule is null || schedule.LearningClassId != classId
            ? null
            : new AcademicScheduleReference(schedule.Id, schedule.LearningClassId,
                schedule.TeacherId, schedule.Status);
    }
}
