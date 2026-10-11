using LearningPlatform.Assessment.Domain;

namespace LearningPlatform.UnitTests;

public sealed class GradingRulesTests
{
    [Fact]
    public void Multiple_choice_grading_requires_the_same_set_of_answers()
    {
        var activity = new AssessmentActivity
        {
            Id = Guid.NewGuid(),
            Questions =
            [
                new ActivityQuestion
                {
                    Id = Guid.NewGuid(),
                    Order = 0,
                    Points = 3m,
                    Type = QuestionType.MultipleChoice,
                    CorrectAnswers = ["A", "C"]
                }
            ]
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Answers = [new SubmissionAnswer(activity.Questions[0].Id, "C,A")]
        };

        var grade = GradingRules.CalculateAutomaticGrade(activity, submission);

        Assert.Equal(3m, grade.Score);
        Assert.True(grade.IsComplete);
    }

    [Fact]
    public void Automatic_grade_is_incomplete_when_subjective_question_exists()
    {
        var activity = new AssessmentActivity
        {
            Id = Guid.NewGuid(),
            Questions =
            [
                new ActivityQuestion
                {
                    Id = Guid.NewGuid(),
                    Order = 0,
                    Points = 5m,
                    Type = QuestionType.LongText
                }
            ]
        };
        var submission = new Submission { Id = Guid.NewGuid(), StudentId = Guid.NewGuid() };

        var grade = GradingRules.CalculateAutomaticGrade(activity, submission);

        Assert.Equal(0m, grade.Score);
        Assert.False(grade.IsComplete);
    }
}
