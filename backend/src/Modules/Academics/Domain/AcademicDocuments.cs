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
