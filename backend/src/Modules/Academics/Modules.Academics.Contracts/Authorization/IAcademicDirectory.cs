using LearningPlatform.Academics.Domain;

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
