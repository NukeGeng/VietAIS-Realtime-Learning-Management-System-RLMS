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
        await ApplyPublishedAsync(session, message.OrganizationId, message.LearningClassId,
            message.CourseId, message.StudentId, message.ActivityId, message.GradeItemId,
            message.GradeEntryId, message.ActivityTitle, message.AttemptNumber, message.Score,
            message.MaxScore, message.GradeVersion, message.PublishedAt,
            message.ResultsReleaseAt, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task Handle(GradeEntryVisibilityChanged message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var progressId = LearningConstants.ProgressId(message.ActivityId, message.StudentId);
        if (!message.IsPublished)
        {
            if (await session.LoadAsync<StudentProgressRecord>(progressId, cancellationToken) is { } progress)
            {
                session.Delete(progress);
                await session.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        await ApplyPublishedAsync(session, message.OrganizationId, message.LearningClassId,
            message.CourseId, message.StudentId, message.ActivityId, Guid.Empty,
            message.GradeEntryId, message.ActivityTitle, message.AttemptNumber, message.Score,
            message.MaxScore, message.GradeVersion, message.ChangedAt,
            message.ResultsReleaseAt, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyPublishedAsync(IDocumentSession session, Guid organizationId,
        Guid learningClassId, Guid? courseId, Guid studentId, Guid activityId, Guid gradeItemId,
        Guid gradeEntryId, string activityTitle, int attemptNumber, decimal score, decimal maxScore,
        int gradeVersion, DateTimeOffset changedAt, DateTimeOffset? resultsReleaseAt,
        CancellationToken cancellationToken)
    {
        var progressId = LearningConstants.ProgressId(activityId, studentId);
        var progress = await session.LoadAsync<StudentProgressRecord>(progressId, cancellationToken);
        var isNew = progress is null;
        if (progress is null)
        {
            progress = new StudentProgressRecord
            {
                Id = progressId,
                OrganizationId = organizationId,
                LearningClassId = learningClassId,
                CourseId = courseId,
                StudentId = studentId,
                ActivityId = activityId,
                ActivityTitle = activityTitle
            };
        }

        if (progress.AttemptNumber > attemptNumber ||
            progress.AttemptNumber == attemptNumber && progress.Version >= gradeVersion)
        {
            return;
        }

        progress.AttemptNumber = attemptNumber;
        progress.CourseId = courseId;
        progress.Score = score;
        progress.MaxScore = maxScore;
        progress.ResultsReleaseAt = resultsReleaseAt;
        progress.Completion = maxScore == 0m
            ? 0m
            : decimal.Round(score * 100m / maxScore, 2);
        progress.Status = LearningProgressStatus.Completed;
        progress.CompletedAt = changedAt;
        progress.UpdatedAt = changedAt;
        progress.Version = gradeVersion;
        if (isNew)
        {
            session.Insert(progress);
        }
        else
        {
            session.Update(progress);
        }

        var historyId = LearningConstants.HistoryId(activityId, studentId, gradeVersion);
        if (gradeItemId != Guid.Empty &&
            await session.LoadAsync<LearningHistoryEntry>(historyId, cancellationToken) is null)
        {
            session.Insert(new LearningHistoryEntry
            {
                Id = historyId,
                OrganizationId = organizationId,
                LearningClassId = learningClassId,
                CourseId = courseId,
                StudentId = studentId,
                ActivityId = activityId,
                GradeEntryId = gradeEntryId,
                GradeVersion = gradeVersion,
                Score = score,
                MaxScore = maxScore,
                RecordedAt = changedAt
            });
        }
    }
}
