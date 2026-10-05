using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;

namespace LearningPlatform.UnitTests;

public sealed class AttemptAvailabilityTests
{
    [Fact]
    public void Expired_in_progress_attempt_is_read_only_without_rewriting_history()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = new StudentActivity();
        attempt.Apply(new StudentActivityStarted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), null, now.AddHours(-2)));
        var definition = new AssessmentActivity { Status = AssessmentActivityStatus.Published, TimeLimit = TimeSpan.FromMinutes(90) };
        var response = StudentActivityResponse.From(attempt, definition);
        Assert.False(response.CanInteract);
        Assert.Contains("hết thời gian", response.InteractionMessage);
        Assert.Equal(attempt.StartedAt.AddMinutes(90), response.ExpiresAt);
        Assert.Equal(StudentActivityStatus.InProgress, attempt.Status);
    }

    [Fact]
    public void Active_attempt_accepts_interaction_and_due_date_caps_time_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = new StudentActivity();
        attempt.Apply(new StudentActivityStarted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), null, now));
        var definition = new AssessmentActivity { Status = AssessmentActivityStatus.Published, TimeLimit = TimeSpan.FromMinutes(90), DueAt = now.AddMinutes(10) };
        Assert.True(StudentActivityResponse.From(attempt, definition).CanInteract);
        Assert.Equal(definition.DueAt, StudentActivityResponse.Deadline(attempt, definition));
        Assert.NotNull(StudentActivityResponse.InteractionBlockReason(attempt, definition, now.AddMinutes(11)));
        definition.Status = AssessmentActivityStatus.Draft;
        Assert.False(StudentActivityResponse.From(attempt, definition).CanInteract);
    }
}
