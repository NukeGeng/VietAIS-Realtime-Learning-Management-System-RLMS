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

public sealed record QuestionBankItemDetailResponse(QuestionBankItemResponse Item,
    QuestionVersionResponse[] Versions)
{
    public static QuestionBankItemDetailResponse From(QuestionBankItem item,
        IEnumerable<QuestionVersion> versions) => new(QuestionBankItemResponse.From(item),
        versions.Select(QuestionVersionResponse.From).ToArray());
}
