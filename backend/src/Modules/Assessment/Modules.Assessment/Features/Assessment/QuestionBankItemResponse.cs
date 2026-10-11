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

public sealed record QuestionBankItemResponse(Guid Id, Guid? CourseId, string Topic, string? Difficulty,
    int CurrentVersion, QuestionBankStatus Status)
{
    public static QuestionBankItemResponse From(QuestionBankItem item) => new(item.Id, item.CourseId,
        item.Topic, item.Difficulty, item.CurrentVersion, item.Status);
}
