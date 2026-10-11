using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Learning.Storage;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Learning.Api;

public sealed record LearningProgressEntryResponse(Guid StudentId, Guid ActivityId,
    string ActivityTitle, decimal Score, decimal MaxScore, decimal Completion,
    LearningProgressStatus Status, DateTimeOffset? CompletedAt, int Version)
{
    public static LearningProgressEntryResponse From(StudentProgressRecord record) => new(record.StudentId,
        record.ActivityId, record.ActivityTitle, record.Score, record.MaxScore, record.Completion,
        record.Status, record.CompletedAt, record.Version);
}
