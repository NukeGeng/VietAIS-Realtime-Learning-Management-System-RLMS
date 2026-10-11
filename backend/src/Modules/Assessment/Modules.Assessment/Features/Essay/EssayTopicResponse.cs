using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Api;

public sealed record EssayTopicResponse(Guid Id, Guid EssayAssignmentId, Guid EssayGroupId,
    string ProposedTitle, string? CanonicalTitle, EssayTopicStatus Status,
    DateTimeOffset SubmittedAt, Guid? ReviewedBy, DateTimeOffset? ReviewedAt)
{
    public static EssayTopicResponse From(EssayTopic topic) => new(topic.Id, topic.EssayAssignmentId,
        topic.EssayGroupId, topic.ProposedTitle, topic.CanonicalTitle, topic.Status, topic.SubmittedAt,
        topic.ReviewedBy, topic.ReviewedAt);
}
