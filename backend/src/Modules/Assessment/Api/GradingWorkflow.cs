using LearningPlatform.Assessment.Domain;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using LearningPlatform.Learning.Contracts;

namespace LearningPlatform.Assessment.Api;

public static class GradingWorkflow
{
    public static async Task<GradeItem> EnsureGradeItemAsync(IDocumentSession session,
        AssessmentActivity activity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = AssessmentConstants.GradeItemId(activity.Id);
        var item = await session.LoadAsync<GradeItem>(id, cancellationToken);
        if (item is not null)
        {
            if (item.MaxScore != activity.Questions.Sum(question => question.Points))
            {
                item.MaxScore = activity.Questions.Sum(question => question.Points);
                item.UpdatedAt = now;
                session.Store(item);
            }

            return item;
        }

        item = new GradeItem
        {
            Id = id,
            OrganizationId = activity.OrganizationId,
            LearningClassId = activity.LearningClassId,
            ActivityId = activity.Id,
            Title = activity.Title,
            MaxScore = activity.Questions.Sum(question => question.Points),
            Weight = 1m,
            Status = GradeItemStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        session.Insert(item);
        return item;
    }

    public static GradeEntry CreateGradeEntry(AssessmentActivity activity, Submission submission,
        GradeItem item, AutomaticGrade automatic, DateTimeOffset now) => new()
        {
            Id = AssessmentConstants.GradeEntryId(activity.Id, submission.StudentId,
            submission.AttemptNumber),
            OrganizationId = activity.OrganizationId,
            LearningClassId = activity.LearningClassId,
            ActivityId = activity.Id,
            GradeItemId = item.Id,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            SourceSubmissionId = submission.Id,
            Score = automatic.Score,
            MaxScore = item.MaxScore,
            Status = GradeEntryStatus.Draft,
            Source = GradeSource.Automatic,
            IsComplete = automatic.IsComplete,
            QuestionResults = automatic.Results,
            Version = 1,
            UpdatedAt = now
        };
}

public static class GradePublicationPublisher
{
    public static async Task PublishAsync(IMessageBus messageBus, ILogger logger,
        GradeEntryPublished message)
    {
        try
        {
            await messageBus.PublishAsync(message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to enqueue learning progress update for grade entry {GradeEntryId}.",
                message.GradeEntryId);
        }

        try
        {
            await messageBus.PublishAsync(new GradeEntryPublishedForReporting(message.OrganizationId,
                message.LearningClassId, message.StudentId, message.GradeEntryId, message.PublishedAt));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to enqueue reporting update for grade entry {GradeEntryId}.",
                message.GradeEntryId);
        }

        var notification = new NotificationRequested(Guid.NewGuid(), message.OrganizationId,
            message.StudentId, "result-published", $"Result published: {message.ActivityTitle}",
            $"Your result is {message.Score:0.##} / {message.MaxScore:0.##}.",
            message.GradeEntryId, message.PublishedAt);
        try
        {
            if (message.ResultsReleaseAt is { } releaseAt && releaseAt > DateTimeOffset.UtcNow)
            {
                await messageBus.ScheduleAsync(notification, releaseAt);
            }
            else
            {
                await messageBus.PublishAsync(notification);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to enqueue result notification for grade entry {GradeEntryId}.",
                message.GradeEntryId);
        }
    }

    public static async Task PublishVisibilityChangedAsync(IMessageBus messageBus, ILogger logger,
        GradeEntryVisibilityChanged message)
    {
        try
        {
            await messageBus.PublishAsync(message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to enqueue learning progress visibility update for {GradeEntryId}.",
                message.GradeEntryId);
        }

        try
        {
            await messageBus.PublishAsync(new GradeEntryVisibilityChangedForReporting(
                message.OrganizationId, message.LearningClassId, message.StudentId));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to enqueue reporting visibility update for {GradeEntryId}.",
                message.GradeEntryId);
        }
    }
}
