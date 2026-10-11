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

public sealed record AssessmentActivityResponse(Guid Id, Guid LearningClassId, Guid? LiveSessionId,
    string Title, string Description, AssessmentActivityKind Kind, AssessmentActivityStatus Status,
    DateTimeOffset? OpensAt, DateTimeOffset? DueAt, TimeSpan? TimeLimit, DateTimeOffset? PublishedAt,
    ActivityQuestionResponse[] Questions, ExamPolicy? ExamPolicy)
{
    public static AssessmentActivityResponse From(AssessmentActivity activity) => From(activity, null);

    public static AssessmentActivityResponse From(AssessmentActivity activity, Guid[]? questionOrder)
    {
        var questionsById = activity.Questions.ToDictionary(question => question.Id);
        var orderedQuestions = questionOrder is { Length: > 0 }
            ? questionOrder.Where(questionsById.ContainsKey).Select(id => questionsById[id])
                .Concat(activity.Questions.Where(question => !questionOrder.Contains(question.Id)))
            : activity.Questions.OrderBy(question => question.Order);
        return new(activity.Id, activity.LearningClassId, activity.LiveSessionId, activity.Title,
            activity.Description, activity.Kind, activity.Status, activity.OpensAt, activity.DueAt,
            activity.TimeLimit, activity.PublishedAt,
            orderedQuestions.Select(ActivityQuestionResponse.From).ToArray(), activity.ExamPolicy);
    }
}
