using LearningPlatform.Classroom.Domain;

namespace LearningPlatform.Assessment.Realtime;

internal static class StudentActivityRealtimeStateFactory
{
    internal static StudentActivityRealtimeState From(StudentActivity activity) => new(activity.Id,
        activity.OrganizationId, activity.StudentId, activity.ActivityId, activity.LearningClassId,
        activity.LiveSessionId, activity.CurrentQuestionId, activity.Status,
        new Dictionary<Guid, string>(activity.Answers), activity.AnsweredCount, activity.StartedAt,
        activity.LastActivityAt, activity.SubmittedAt, activity.Version, activity.QuestionOrder.ToArray(), activity.AttemptNumber,
        activity.HelpRequested, activity.HelpRequestedAt, activity.HelpAcknowledgedAt);
}
