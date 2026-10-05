namespace LearningPlatform.Reporting.Domain;

public sealed class ClassReportSnapshot
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid CourseId { get; set; }
    public int EnrollmentCount { get; set; }
    public int ActiveEnrollmentCount { get; set; }
    public int CompletedEnrollmentCount { get; set; }
    public int ActivityCount { get; set; }
    public int PublishedActivityCount { get; set; }
    public int SubmissionCount { get; set; }
    public int PublishedGradeCount { get; set; }
    public decimal AverageScorePercent { get; set; }
    public int AttendanceSessionCount { get; set; }
    public int AttendancePresentCount { get; set; }
    public int AttendanceLateCount { get; set; }
    public int AttendanceAbsentCount { get; set; }
    public int AttendanceExcusedCount { get; set; }
    public decimal AttendancePercentage { get; set; }
    public int OverviewStudentCount { get; set; }
    public int OverviewInProgressCount { get; set; }
    public int OverviewSubmittedCount { get; set; }
    public int OverviewHelpRequestedCount { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StudentReportSnapshot
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid StudentId { get; set; }
    public int PublishedGradeCount { get; set; }
    public decimal AverageScorePercent { get; set; }
    public int CompletedActivityCount { get; set; }
    public int ActivityCount { get; set; }
    public decimal ActivityCompletionPercent { get; set; }
    public int LessonCount { get; set; }
    public int CompletedLessonCount { get; set; }
    public decimal LessonCompletionPercent { get; set; }
    public int AttendanceSessionCount { get; set; }
    public int AttendancePresentCount { get; set; }
    public int AttendanceLateCount { get; set; }
    public int AttendanceAbsentCount { get; set; }
    public int AttendanceExcusedCount { get; set; }
    public decimal AttendancePercentage { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
