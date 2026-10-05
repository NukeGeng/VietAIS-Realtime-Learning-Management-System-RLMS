namespace LearningPlatform.Classroom.Domain;

public static class LiveSessionRules
{
    public static string RequireMediaRoomId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            throw new LiveSessionValidationException("Media room ID must be present and at most 128 characters.");
        }

        return value;
    }
}

public sealed class LiveSessionValidationException(string message) : Exception(message);
