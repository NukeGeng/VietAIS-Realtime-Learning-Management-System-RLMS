namespace LearningPlatform.Classroom.Domain;

public static class ClassroomInteractionRules
{
    public const int MaxAttendanceAdjustmentReasonLength = 500;

    public static string RequireAdjustmentReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ClassroomInteractionValidationException(
                "An attendance adjustment reason is required.");
        }

        if (normalized.Length > MaxAttendanceAdjustmentReasonLength)
        {
            throw new ClassroomInteractionValidationException(
                $"An attendance adjustment reason cannot exceed {MaxAttendanceAdjustmentReasonLength} characters.");
        }

        return normalized;
    }
}

public sealed class ClassroomInteractionValidationException(string message) : Exception(message);
