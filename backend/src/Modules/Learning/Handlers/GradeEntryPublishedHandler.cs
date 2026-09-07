using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using Marten;

namespace LearningPlatform.Learning.Handlers;

public static class GradeEntryPublishedHandler
{
    public static async Task Handle(GradeEntryPublished message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var progressId = LearningConstants.ProgressId(message.ActivityId, message.StudentId);
        var progress = await session.LoadAsync<StudentProgressRecord>(progressId, cancellationToken);
        var isNew = progress is null;
        if (progress is null)
        {
            progress = new StudentProgressRecord
            {
                Id = progressId,
                OrganizationId = message.OrganizationId,
                LearningClassId = message.LearningClassId,
                CourseId = message.CourseId,
                StudentId = message.StudentId,
                ActivityId = message.ActivityId,
                ActivityTitle = message.ActivityTitle
            };
        }

        if (progress.AttemptNumber > message.AttemptNumber ||
            progress.AttemptNumber == message.AttemptNumber && progress.Version >= message.GradeVersion)
        {
            return;
        }

        progress.AttemptNumber = message.AttemptNumber;
        progress.CourseId = message.CourseId;
        progress.Score = message.Score;
        progress.MaxScore = message.MaxScore;
        progress.ResultsReleaseAt = message.ResultsReleaseAt;
        progress.Completion = message.MaxScore == 0m
            ? 0m
            : decimal.Round(message.Score * 100m / message.MaxScore, 2);
        progress.Status = LearningProgressStatus.Completed;
        progress.CompletedAt = message.PublishedAt;
        progress.UpdatedAt = message.PublishedAt;
        progress.Version = message.GradeVersion;
        if (isNew)
        {
            session.Insert(progress);
        }
        else
        {
            session.Update(progress);
        }

        var historyId = LearningConstants.HistoryId(message.ActivityId, message.StudentId,
            message.GradeVersion);
        if (await session.LoadAsync<LearningHistoryEntry>(historyId, cancellationToken) is null)
        {
            session.Insert(new LearningHistoryEntry
            {
                Id = historyId,
                OrganizationId = message.OrganizationId,
                LearningClassId = message.LearningClassId,
                CourseId = message.CourseId,
                StudentId = message.StudentId,
                ActivityId = message.ActivityId,
                GradeEntryId = message.GradeEntryId,
                GradeVersion = message.GradeVersion,
                Score = message.Score,
                MaxScore = message.MaxScore,
                RecordedAt = message.PublishedAt
            });
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
