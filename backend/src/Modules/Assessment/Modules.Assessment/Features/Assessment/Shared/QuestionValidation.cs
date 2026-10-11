using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityPublication;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityNotifications;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptTransitions;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AssessmentAccess;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptAvailability;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptSnapshots;
using static LearningPlatform.Assessment.Features.Assessment.Shared.QuestionValidation;

namespace LearningPlatform.Assessment.Features.Assessment.Shared;

internal static class QuestionValidation
{


    internal static (DateTimeOffset? OpensAt, DateTimeOffset? DueAt, TimeSpan? TimeLimit) ValidateActivityTimes(
        DateTimeOffset? opensAt, DateTimeOffset? dueAt, TimeSpan? timeLimit)
    {
        if (opensAt is not null && dueAt is not null && dueAt <= opensAt)
        {
            throw new AssessmentValidationException("The activity due time must be after its opening time.");
        }

        if (timeLimit is { } limit && (limit <= TimeSpan.Zero || limit > TimeSpan.FromDays(30)))
        {
            throw new AssessmentValidationException("The activity time limit must be positive and at most 30 days.");
        }

        return (opensAt, dueAt, timeLimit);
    }



    internal static QuestionOption[] NormalizeOptions(QuestionOptionRequest[]? options)
    {
        return (options ?? [])
            .Select(option => option is null
                ? throw new AssessmentValidationException("Question options cannot be null.")
                : new QuestionOption(option.Key?.Trim() ?? "", option.Text?.Trim() ?? ""))
            .ToArray();
    }



    internal static string[] NormalizeAnswers(string[]? answers) =>
        (answers ?? []).Select(answer => answer is null
                ? throw new AssessmentValidationException("Question answers cannot be null.")
                : answer.Trim())
            .Where(answer => answer.Length > 0).ToArray();



    internal static string NormalizeAnswer(ActivityQuestion question, string value)
    {
        if (question.Type == QuestionType.TrueFalse)
        {
            if (!new[] { "true", "false" }.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                throw new AssessmentValidationException("The answer must be true or false.");
            }

            return value.ToLowerInvariant();
        }

        if (question.Type == QuestionType.SingleChoice)
        {
            var option = question.Options.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, value, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                throw new AssessmentValidationException("The answer is not one of the available options.");
            }

            return option.Key;
        }

        if (question.Type == QuestionType.MultipleChoice)
        {
            var values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0)
            {
                throw new AssessmentValidationException("At least one option must be selected.");
            }

            var keys = values.Select(selected => question.Options.FirstOrDefault(option =>
                    string.Equals(option.Key, selected, StringComparison.OrdinalIgnoreCase))?.Key)
                .ToArray();
            if (keys.Any(key => key is null) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            {
                throw new AssessmentValidationException("The answer contains an unavailable or repeated option.");
            }

            return string.Join(',', keys.Order(StringComparer.Ordinal));
        }

        return value;
    }

}
