namespace LearningPlatform.Assessment.Domain;

public static class GradingRules
{
    public const int MaxFeedbackLength = 20_000;
    public const int MaxRubricCriteria = 100;
    public const int MaxRubricNameLength = 200;
    public const int MaxRubricDescriptionLength = 4_000;
    public const int MaxAdjustmentReasonLength = 2_000;

    public static decimal RequireWeight(decimal value)
    {
        if (value < 0m || value > 100m || decimal.Round(value, 4) != value)
        {
            throw new AssessmentValidationException("Grade item weight must be between 0 and 100.");
        }

        return value;
    }

    public static string RequireAdjustmentReason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length == 0 || reason.Length > MaxAdjustmentReasonLength)
        {
            throw new AssessmentValidationException(
                "A grade adjustment reason is required and must be at most 2000 characters.");
        }

        return reason;
    }

    public static bool IsObjective(QuestionType type) => type is QuestionType.SingleChoice or
        QuestionType.MultipleChoice or QuestionType.TrueFalse;

    public static AutomaticGrade CalculateAutomaticGrade(AssessmentActivity activity,
        Submission submission)
    {
        var answers = submission.Answers.ToDictionary(answer => answer.QuestionId, answer => answer.Value);
        var results = activity.Questions.OrderBy(question => question.Order).Select(question =>
        {
            answers.TryGetValue(question.Id, out var answer);
            var score = IsCorrect(question, answer) ? question.Points : 0m;
            return new GradeQuestionResult(question.Id, score, question.Points, null, null);
        }).ToArray();
        var hasSubjective = activity.Questions.Any(question => !IsObjective(question.Type));
        return new AutomaticGrade(results, results.Sum(result => result.Score), !hasSubjective);
    }

    public static void ValidateRubric(IEnumerable<RubricCriterion> criteria)
    {
        var values = criteria.ToArray();
        if (values.Length == 0 || values.Length > MaxRubricCriteria)
        {
            throw new AssessmentValidationException("A rubric needs between 1 and 100 criteria.");
        }

        if (values.Select(value => value.Order).Distinct().Count() != values.Length ||
            values.Select(value => value.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
        {
            throw new AssessmentValidationException("Rubric criteria need unique names and orders.");
        }

        foreach (var criterion in values)
        {
            if (criterion.Id == Guid.Empty || criterion.Order < 0 || criterion.MaxPoints <= 0 ||
                criterion.MaxPoints > 100_000 || string.IsNullOrWhiteSpace(criterion.Name) ||
                criterion.Name.Trim().Length > MaxRubricNameLength ||
                (criterion.Description is not null &&
                    criterion.Description.Trim().Length > MaxRubricDescriptionLength))
            {
                throw new AssessmentValidationException("Each rubric criterion needs a valid name, order, description and positive max points.");
            }
        }
    }

    public static string? NormalizeFeedback(string? feedback)
    {
        var value = feedback?.Trim();
        if (value is not null && value.Length > MaxFeedbackLength)
        {
            throw new AssessmentValidationException("Feedback is too long.");
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool IsCorrect(ActivityQuestion question, string? answer)
    {
        if (!IsObjective(question.Type) || string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        if (question.Type == QuestionType.MultipleChoice)
        {
            var selected = answer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToUpperInvariant()).Order(StringComparer.Ordinal).ToArray();
            var correct = (question.CorrectAnswers ?? []).Select(value => value.Trim().ToUpperInvariant())
                .Order(StringComparer.Ordinal).ToArray();
            return selected.SequenceEqual(correct, StringComparer.Ordinal);
        }

        return string.Equals(answer.Trim(), question.CorrectAnswers?.SingleOrDefault()?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AutomaticGrade(GradeQuestionResult[] Results, decimal Score, bool IsComplete);
