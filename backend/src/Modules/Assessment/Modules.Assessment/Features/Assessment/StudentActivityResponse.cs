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

namespace LearningPlatform.Assessment.Api;

public sealed record StudentActivityResponse(Guid Id, Guid StudentId, Guid ActivityId,
    Guid LearningClassId, Guid? LiveSessionId, Guid? CurrentQuestionId, StudentActivityStatus Status,
    Dictionary<Guid, string> Answers, int AnsweredCount, DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt, DateTimeOffset? SubmittedAt, long Version, int AttemptNumber,
    Guid[] QuestionOrder, bool HelpRequested, DateTimeOffset? HelpRequestedAt,
    DateTimeOffset? HelpAcknowledgedAt)
{
    public bool CanInteract { get; init; }
    public string? InteractionMessage { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public static StudentActivityResponse From(StudentActivity activity, AssessmentActivity? definition = null)
    {
        var reason = definition is null ? null : InteractionBlockReason(activity, definition,
            DateTimeOffset.UtcNow);
        return new StudentActivityResponse(activity.Id, activity.StudentId, activity.ActivityId,
            activity.LearningClassId, activity.LiveSessionId, activity.CurrentQuestionId,
            activity.Status, new Dictionary<Guid, string>(activity.Answers), activity.AnsweredCount,
            activity.StartedAt, activity.LastActivityAt, activity.SubmittedAt, activity.Version,
            activity.AttemptNumber, activity.QuestionOrder.ToArray(), activity.HelpRequested,
            activity.HelpRequestedAt, activity.HelpAcknowledgedAt)
        {
            CanInteract = definition is not null && reason is null,
            InteractionMessage = reason,
            ExpiresAt = definition is null ? null : Deadline(activity, definition)
        };
    }

    public static DateTimeOffset? Deadline(StudentActivity attempt, AssessmentActivity definition)
    {
        var deadline = definition.TimeLimit is { } limit ? attempt.StartedAt + limit : (DateTimeOffset?)null;
        return definition.DueAt is { } due && (deadline is null || due < deadline) ? due : deadline;
    }

    public static string? InteractionBlockReason(StudentActivity attempt, AssessmentActivity definition, DateTimeOffset now)
    {
        if (attempt.Status is StudentActivityStatus.Submitted or StudentActivityStatus.Closed)
            return "Bài làm đã kết thúc.";
        if (Deadline(attempt, definition) is { } deadline && now > deadline)
            return "Đã hết thời gian làm bài. Bạn có thể xem lại câu trả lời đã lưu.";
        if (definition.Status != AssessmentActivityStatus.Published)
            return "Hoạt động đang tạm dừng hoặc đã đóng.";
        if (definition.OpensAt is { } opens && now < opens)
            return "Chưa đến giờ làm bài.";
        if (attempt.Status != StudentActivityStatus.InProgress)
            return "Bài làm đang tạm dừng. Nhấn Tiếp tục để làm bài.";
        return null;
    }
}
