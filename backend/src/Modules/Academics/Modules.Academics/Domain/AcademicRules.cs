using System.Text;

namespace LearningPlatform.Academics.Domain;

public static class AcademicRules
{
    public static string NormalizeCourseCode(string? value)
    {
        var code = (value ?? "").Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (code.Length is < 2 or > 32 || code.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_')))
        {
            throw new AcademicValidationException(
                "Course code must be 2-32 letters, numbers, '-' or '_'.");
        }

        return code;
    }

    public static string RequireTitle(string? value)
    {
        var title = (value ?? "").Trim().Normalize(NormalizationForm.FormKC);
        if (title.Length is < 1 or > 200)
        {
            throw new AcademicValidationException("Title must be between 1 and 200 characters.");
        }

        return title;
    }

    public static string RequireDescription(string? value)
    {
        var description = (value ?? "").Trim().Normalize(NormalizationForm.FormKC);
        if (description.Length > 4000)
        {
            throw new AcademicValidationException("Description cannot exceed 4000 characters.");
        }

        return description;
    }

    public static string RequireContentSummary(string? value)
    {
        var summary = (value ?? "").Trim().Normalize(NormalizationForm.FormKC);
        if (summary.Length > 2000)
        {
            throw new AcademicValidationException("Content summary cannot exceed 2000 characters.");
        }

        return summary;
    }

    public static int RequireOrder(int value)
    {
        if (value < 0)
        {
            throw new AcademicValidationException("Order must be zero or greater.");
        }

        return value;
    }

    public static (DateOnly Start, DateOnly End) RequireDateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new AcademicValidationException("End date must be on or after the start date.");
        }

        return (start, end);
    }

    public static (DateTimeOffset Start, DateTimeOffset End) RequireTimeRange(
        DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new AcademicValidationException("Schedule end must be after schedule start.");
        }

        return (start, end);
    }

    public static string? RequireRecurrence(string? recurrence)
    {
        var value = string.IsNullOrWhiteSpace(recurrence)
            ? null
            : recurrence.Trim().Normalize(NormalizationForm.FormKC);
        if (value is not null && value.Length > 500)
        {
            throw new AcademicValidationException("Recurrence cannot exceed 500 characters.");
        }

        return value;
    }
}

public sealed class AcademicValidationException(string message) : Exception(message);
