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

public sealed record ActivityQuestionResponse(Guid Id, Guid QuestionVersionId, int Order, decimal Points,
    string Prompt, QuestionType Type, QuestionOption[] Options)
{
    public static ActivityQuestionResponse From(ActivityQuestion question) => new(question.Id,
        question.QuestionVersionId, question.Order, question.Points, question.Prompt, question.Type,
        question.Options);
}
