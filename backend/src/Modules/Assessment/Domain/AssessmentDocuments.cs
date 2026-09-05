using LearningPlatform.Classroom.Domain;

namespace LearningPlatform.Assessment.Domain;

public enum QuestionBankStatus
{
    Active,
    Archived
}

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    TrueFalse,
    ShortText,
    LongText
}

public sealed class QuestionBankItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CourseId { get; set; }
    public string Topic { get; set; } = "";
    public string? Difficulty { get; set; }
    public int CurrentVersion { get; set; }
    public QuestionBankStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class QuestionVersion
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid QuestionBankItemId { get; set; }
    public int Version { get; set; }
    public string Prompt { get; set; } = "";
    public QuestionType Type { get; set; }
    public QuestionOption[] Options { get; set; } = [];
    public string[] CorrectAnswers { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed record QuestionOption(string Key, string Text);

public enum AssessmentActivityKind
{
    Assignment,
    Exercise,
    Quiz
}

public enum AssessmentActivityStatus
{
    Draft,
    Published,
    Paused,
    Closed
}

public sealed class AssessmentActivity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? LiveSessionId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public AssessmentActivityKind Kind { get; set; }
    public AssessmentActivityStatus Status { get; set; }
    public DateTimeOffset? OpensAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public TimeSpan? TimeLimit { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ActivityQuestion> Questions { get; set; } = [];
}

/// <summary>
/// Immutable question snapshot embedded in an activity at selection time.
/// </summary>
public sealed class ActivityQuestion
{
    public Guid Id { get; set; }
    public Guid QuestionVersionId { get; set; }
    public int Order { get; set; }
    public decimal Points { get; set; }
    public string Prompt { get; set; } = "";
    public QuestionType Type { get; set; }
    public QuestionOption[] Options { get; set; } = [];
    public string[] CorrectAnswers { get; set; } = [];
}

public enum SubmissionStatus
{
    Submitted
}

public sealed class Submission
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid StudentId { get; set; }
    public Guid StudentActivityId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public SubmissionStatus Status { get; set; }
    public int Version { get; set; }
    public SubmissionAnswer[] Answers { get; set; } = [];
}

public sealed record SubmissionAnswer(Guid QuestionId, string Value);

public sealed class ClassOverviewView
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid ActivityId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ClassOverviewStudent> Students { get; set; } = [];
}

public sealed class ClassOverviewStudent
{
    public Guid StudentId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid? CurrentQuestionId { get; set; }
    public int AnsweredCount { get; set; }
    public int TotalQuestions { get; set; }
    public decimal Progress { get; set; }
    public StudentActivityStatus? Status { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public bool HelpRequested { get; set; }
    public long Version { get; set; }
}

public sealed class StudentAttemptDetailView
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? LiveSessionId { get; set; }
    public Guid? CurrentQuestionId { get; set; }
    public StudentActivityStatus Status { get; set; }
    public Dictionary<Guid, string> Answers { get; set; } = [];
    public int AnsweredCount { get; set; }
    public int TotalQuestions { get; set; }
    public decimal Progress { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public long Version { get; set; }
}
