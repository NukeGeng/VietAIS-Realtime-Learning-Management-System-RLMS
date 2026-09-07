namespace LearningPlatform.Classroom.Domain;

public enum StudentActivityStatus
{
    InProgress,
    Paused,
    Submitted,
    Closed
}

/// <summary>
/// Event-sourced state for one student's attempt on one assessment activity.
/// </summary>
public sealed class StudentActivity
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
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public long Version { get; set; }
    public int AttemptNumber { get; set; } = 1;

    public void Apply(StudentActivityStarted @event)
    {
        Id = @event.AttemptId;
        OrganizationId = @event.OrganizationId;
        StudentId = @event.StudentId;
        ActivityId = @event.ActivityId;
        LearningClassId = @event.LearningClassId;
        LiveSessionId = @event.LiveSessionId;
        AttemptNumber = @event.AttemptNumber;
        Status = StudentActivityStatus.InProgress;
        StartedAt = @event.OccurredAt;
        LastActivityAt = @event.OccurredAt;
        Version = 1;
    }

    public void Apply(QuestionOpened @event)
    {
        CurrentQuestionId = @event.QuestionId;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(AnswerSubmitted @event)
    {
        Answers[@event.QuestionId] = @event.Value;
        AnsweredCount = Answers.Count;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(AnswerChanged @event)
    {
        Answers[@event.QuestionId] = @event.Value;
        AnsweredCount = Answers.Count;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(StudentActivityPaused @event)
    {
        Status = StudentActivityStatus.Paused;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(StudentActivityResumed @event)
    {
        Status = StudentActivityStatus.InProgress;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(ActivitySubmitted @event)
    {
        Status = StudentActivityStatus.Submitted;
        SubmittedAt = @event.OccurredAt;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }

    public void Apply(StudentActivityClosed @event)
    {
        Status = StudentActivityStatus.Closed;
        LastActivityAt = @event.OccurredAt;
        Version++;
    }
}

public sealed record StudentActivityStarted(Guid AttemptId, Guid OrganizationId, Guid StudentId,
    Guid ActivityId, Guid LearningClassId, Guid? LiveSessionId, DateTimeOffset OccurredAt,
    int AttemptNumber = 1);

public sealed record QuestionOpened(Guid AttemptId, Guid QuestionId, DateTimeOffset OccurredAt);

public sealed record AnswerSubmitted(Guid AttemptId, Guid QuestionId, string Value,
    DateTimeOffset OccurredAt);

public sealed record AnswerChanged(Guid AttemptId, Guid QuestionId, string Value,
    DateTimeOffset OccurredAt);

public sealed record StudentActivityPaused(Guid AttemptId, DateTimeOffset OccurredAt);

public sealed record StudentActivityResumed(Guid AttemptId, DateTimeOffset OccurredAt);

public sealed record ActivitySubmitted(Guid AttemptId, DateTimeOffset OccurredAt);

public sealed record StudentActivityClosed(Guid AttemptId, DateTimeOffset OccurredAt);
