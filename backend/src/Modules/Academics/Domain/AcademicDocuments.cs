namespace LearningPlatform.Academics.Domain;

public enum CourseStatus
{
    Draft,
    Published,
    Archived
}

public enum AcademicContentStatus
{
    Draft,
    Published,
    Archived
}

public sealed class Course
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public CourseStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CourseModule
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public AcademicContentStatus Status { get; set; }
}

public sealed class Lesson
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CourseId { get; set; }
    public Guid CourseModuleId { get; set; }
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public string ContentSummary { get; set; } = "";
    public AcademicContentStatus Status { get; set; }
}

public enum LearningClassStatus
{
    Preparing,
    Active,
    Paused,
    Completed,
    Archived
}

public sealed class LearningClass
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CourseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public LearningClassStatus Status { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EnrollmentStatus
{
    Active,
    Dropped,
    Completed
}

public sealed class Enrollment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid StudentId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum TeacherAssignmentStatus
{
    Active,
    Removed
}

public sealed class ClassTeacherAssignment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid TeacherId { get; set; }
    public TeacherAssignmentStatus Status { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

public enum ScheduleStatus
{
    Scheduled,
    Rescheduled,
    Cancelled,
    Completed
}

public sealed class ClassSchedule
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid TeacherId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Recurrence { get; set; }
    public ScheduleStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
