namespace LearningPlatform.Academics.Domain;



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

/// <summary>
/// A concrete learning-content structure that can be assigned to one or more
/// learning classes. Course remains the academic subject; curriculum owns the
/// learner-facing content context.
/// </summary>
public sealed class Curriculum
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public AcademicContentStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}


public sealed class ClassCurriculumAssignment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid CurriculumId { get; set; }
    public int Order { get; set; }
    public ClassCurriculumAssignmentStatus Status { get; set; }
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
