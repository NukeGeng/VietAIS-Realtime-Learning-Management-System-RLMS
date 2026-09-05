namespace LearningPlatform.Assessment.Domain;

public static class AssessmentRules
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 10_000;
    public const int MaxTopicLength = 200;
    public const int MaxDifficultyLength = 64;
    public const int MaxPromptLength = 20_000;
    public const int MaxOptionKeyLength = 32;
    public const int MaxOptionTextLength = 1_000;
    public const int MaxAnswerLength = 20_000;
    public const int MaxQuestionsPerActivity = 500;

    public static string RequireTitle(string? value)
    {
        var normalized = RequireText(value, "Activity title", MaxTitleLength);
        return normalized;
    }

    public static string RequireDescription(string? value)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length > MaxDescriptionLength)
        {
            throw new AssessmentValidationException("Activity description is too long.");
        }

        return normalized;
    }

    public static string RequireTopic(string? value) =>
        RequireText(value, "Question topic", MaxTopicLength);

    public static string? NormalizeDifficulty(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaxDifficultyLength)
        {
            throw new AssessmentValidationException("Question difficulty is too long.");
        }

        return normalized;
    }

    public static string RequirePrompt(string? value) =>
        RequireText(value, "Question prompt", MaxPromptLength);

    public static string RequireAnswer(string? value) =>
        RequireText(value, "Answer", MaxAnswerLength);

    public static decimal RequirePoints(decimal value)
    {
        if (value <= 0 || value > 100_000 || decimal.Round(value, 4) != value)
        {
            throw new AssessmentValidationException("Question points must be positive and at most 100000.");
        }

        return value;
    }

    public static void ValidateQuestion(QuestionType type, QuestionOption[]? options,
        string[]? correctAnswers)
    {
        if (!Enum.IsDefined(type))
        {
            throw new AssessmentValidationException("The question type is not supported.");
        }

        var safeOptions = options ?? [];
        var safeAnswers = correctAnswers ?? [];
        var keys = safeOptions.Select(option => option.Key).ToArray();
        if (safeOptions.Length > 100 || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
        {
            throw new AssessmentValidationException("Question options must have unique keys.");
        }

        foreach (var option in safeOptions)
        {
            if (string.IsNullOrWhiteSpace(option.Key) || option.Key.Trim().Length > MaxOptionKeyLength ||
                string.IsNullOrWhiteSpace(option.Text) || option.Text.Trim().Length > MaxOptionTextLength)
            {
                throw new AssessmentValidationException("Each question option needs a key and text within the configured limits.");
            }
        }

        if (type is QuestionType.SingleChoice or QuestionType.MultipleChoice)
        {
            if (safeOptions.Length == 0 || safeAnswers.Length == 0 ||
                safeAnswers.Any(answer => !keys.Contains(answer, StringComparer.OrdinalIgnoreCase)))
            {
                throw new AssessmentValidationException("Choice questions need valid options and correct answers.");
            }

            if (type == QuestionType.SingleChoice && safeAnswers.Length != 1)
            {
                throw new AssessmentValidationException("A single-choice question needs exactly one correct answer.");
            }
        }
        else if (type == QuestionType.TrueFalse)
        {
            if (safeAnswers.Length != 1 ||
                !new[] { "true", "false" }.Contains(safeAnswers[0], StringComparer.OrdinalIgnoreCase))
            {
                throw new AssessmentValidationException("A true/false question needs true or false as its correct answer.");
            }
        }
        else if (safeAnswers.Length != 0)
        {
            throw new AssessmentValidationException("Text questions cannot define an objective correct answer.");
        }
    }

    public static AssessmentActivityKind RequireActivityKind(AssessmentActivityKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new AssessmentValidationException("The activity kind is not supported.");
        }

        return kind;
    }

    private static string RequireText(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length == 0)
        {
            throw new AssessmentValidationException($"{name} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new AssessmentValidationException($"{name} is too long.");
        }

        return normalized;
    }
}

public sealed class AssessmentValidationException(string message) : Exception(message);
