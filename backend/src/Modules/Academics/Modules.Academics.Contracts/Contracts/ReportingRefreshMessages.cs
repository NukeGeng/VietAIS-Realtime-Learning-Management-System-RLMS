namespace LearningPlatform.Academics.Contracts;

/// <summary>
/// Refreshes class reporting after an enrollment changes. The student report is
/// refreshed only when the enrollment is active so a dropped enrollment cannot
/// create a new student-facing snapshot.
/// </summary>
public sealed record EnrollmentChangedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId, LearningPlatform.Academics.Domain.EnrollmentStatus Status);

/// <summary>
/// Refreshes student reporting after published course content changes the
/// lesson set used by class progress snapshots.
/// </summary>
public sealed record CourseContentChangedForReporting(Guid OrganizationId, Guid CourseId);
