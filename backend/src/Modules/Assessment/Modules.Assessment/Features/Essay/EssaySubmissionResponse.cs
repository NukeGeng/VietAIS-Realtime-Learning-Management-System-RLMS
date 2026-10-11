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

public sealed record EssaySubmissionResponse(Guid Id, Guid EssayAssignmentId, Guid EssayGroupId,
    Guid FileAssetId, int Version, EssaySubmissionStatus Status, DateTimeOffset UploadedAt,
    DateTimeOffset? SubmittedAt, string[] Errors)
{
    public static EssaySubmissionResponse From(EssaySubmission submission, string[] errors) => new(
        submission.Id, submission.EssayAssignmentId, submission.EssayGroupId, submission.FileAssetId,
        submission.Version, submission.Status, submission.UploadedAt, submission.SubmittedAt, errors);
}
