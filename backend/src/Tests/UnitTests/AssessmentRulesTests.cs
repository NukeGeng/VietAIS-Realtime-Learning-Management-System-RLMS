using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;

namespace LearningPlatform.UnitTests;

public sealed class AssessmentRulesTests
{
    [Fact]
    public void Choice_question_requires_valid_options_and_answers()
    {
        var options = new[]
        {
            new QuestionOption("A", "First"),
            new QuestionOption("B", "Second")
        };

        AssessmentRules.ValidateQuestion(QuestionType.SingleChoice, options, ["A"]);
        Assert.Throws<AssessmentValidationException>(() =>
            AssessmentRules.ValidateQuestion(QuestionType.SingleChoice, options, ["A", "B"]));
        Assert.Throws<AssessmentValidationException>(() =>
            AssessmentRules.ValidateQuestion(QuestionType.MultipleChoice, options, ["C"]));
        Assert.Throws<AssessmentValidationException>(() =>
            AssessmentRules.ValidateQuestion(QuestionType.ShortText, [], ["A"]));
    }

    [Fact]
    public void Student_activity_replay_keeps_current_question_answers_and_submission_state()
    {
        var attemptId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var firstQuestion = Guid.NewGuid();
        var secondQuestion = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var activity = new StudentActivity();

        activity.Apply(new StudentActivityStarted(attemptId, organizationId, studentId, activityId,
            Guid.NewGuid(), null, startedAt));
        activity.Apply(new QuestionOpened(attemptId, firstQuestion, startedAt.AddSeconds(1)));
        activity.Apply(new AnswerSubmitted(attemptId, firstQuestion, "A", startedAt.AddSeconds(2)));
        activity.Apply(new AnswerChanged(attemptId, firstQuestion, "B", startedAt.AddSeconds(3)));
        activity.Apply(new QuestionOpened(attemptId, secondQuestion, startedAt.AddSeconds(4)));
        activity.Apply(new ActivitySubmitted(attemptId, startedAt.AddSeconds(5)));

        Assert.Equal(StudentActivityStatus.Submitted, activity.Status);
        Assert.Equal(secondQuestion, activity.CurrentQuestionId);
        Assert.Equal("B", activity.Answers[firstQuestion]);
        Assert.Equal(1, activity.AnsweredCount);
        Assert.Equal(startedAt.AddSeconds(5), activity.SubmittedAt);
        Assert.Equal(6, activity.Version);
    }
}
