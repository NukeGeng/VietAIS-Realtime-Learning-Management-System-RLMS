namespace LearningPlatform.Communication.Domain;

public static class ChatRules
{
    public const int MaxMessageLength = 2_000;

    public static string RequireMessage(string? body)
    {
        var normalized = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ChatValidationException("A chat message is required.");
        }

        if (normalized.Length > MaxMessageLength)
        {
            throw new ChatValidationException($"A chat message cannot exceed {MaxMessageLength} characters.");
        }

        return normalized;
    }
}

public sealed class ChatValidationException(string message) : Exception(message);
