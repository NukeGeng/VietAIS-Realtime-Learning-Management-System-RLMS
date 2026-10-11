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

public sealed record QuestionVersionResponse(Guid Id, Guid QuestionBankItemId, int Version,
    string Prompt, QuestionType Type, QuestionOption[] Options, string[] CorrectAnswers)
{
    public static QuestionVersionResponse From(QuestionVersion version) => new(version.Id,
        version.QuestionBankItemId, version.Version, version.Prompt, version.Type, version.Options,
        version.CorrectAnswers);
}
