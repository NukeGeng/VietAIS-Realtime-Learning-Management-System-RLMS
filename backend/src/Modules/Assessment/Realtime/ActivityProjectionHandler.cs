using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using Marten;

namespace LearningPlatform.Assessment.Realtime;

public static class ActivityProjectionHandler
{
    public static async Task Handle(StudentActivityChanged message, IDocumentStore store,
        IActivityRealtimeNotifier notifier, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var activity = await session.LoadAsync<AssessmentActivity>(message.ActivityId, cancellationToken);
        var state = message.State;
        if (activity is null || state.OrganizationId != message.OrganizationId ||
            state.AttemptId != message.AttemptId || state.ActivityId != message.ActivityId ||
            state.StudentId != message.StudentId || state.LearningClassId != message.LearningClassId)
        {
            return;
        }

        var totalQuestions = activity.Questions.Count;
        var progress = totalQuestions == 0
            ? 0m
            : decimal.Round(state.AnsweredCount * 100m / totalQuestions, 2);
        var existingDetail = await session.LoadAsync<StudentAttemptDetailView>(state.AttemptId,
            cancellationToken);
        var detailNeedsUpdate = existingDetail is null || existingDetail.Version < state.Version;
        if (detailNeedsUpdate)
        {
            var detail = new StudentAttemptDetailView
            {
                Id = state.AttemptId,
                OrganizationId = state.OrganizationId,
                StudentId = state.StudentId,
                AttemptNumber = state.AttemptNumber,
                ActivityId = state.ActivityId,
                LearningClassId = state.LearningClassId,
                LiveSessionId = state.LiveSessionId,
                CurrentQuestionId = state.CurrentQuestionId,
                QuestionOrder = state.QuestionOrder?.ToArray() ?? [],
                Status = state.Status,
                Answers = new Dictionary<Guid, string>(state.Answers),
                HelpRequested = state.HelpRequested,
                HelpRequestedAt = state.HelpRequestedAt,
                HelpAcknowledgedAt = state.HelpAcknowledgedAt,
                AnsweredCount = state.AnsweredCount,
                TotalQuestions = totalQuestions,
                Progress = progress,
                StartedAt = state.StartedAt,
                LastActivityAt = state.LastActivityAt,
                SubmittedAt = state.SubmittedAt,
                Version = state.Version
            };
            session.Store(detail);
        }

        var overviewId = AssessmentConstants.ClassOverviewViewId(state.LearningClassId,
            state.ActivityId);
        var overview = await session.LoadAsync<ClassOverviewView>(overviewId, cancellationToken) ??
            new ClassOverviewView
            {
                Id = overviewId,
                OrganizationId = state.OrganizationId,
                LearningClassId = state.LearningClassId,
                ActivityId = state.ActivityId
            };
        overview.Students ??= [];
        var student = overview.Students.FirstOrDefault(item => item.StudentId == state.StudentId);
        var overviewChanged = false;
        if (student is null)
        {
            student = new ClassOverviewStudent { StudentId = state.StudentId };
            overview.Students.Add(student);
        }
        if (student.AttemptNumber < state.AttemptNumber ||
            student.AttemptNumber == state.AttemptNumber &&
            (student.Version < state.Version || student.AttemptId == Guid.Empty))
        {
            var previousAnswered = (student.AnsweredQuestionIds ??
                existingDetail?.Answers.Keys.ToArray() ?? []).ToHashSet();
            var currentAnswered = state.Answers.Keys.ToHashSet();
            var previousCurrentQuestion = student.CurrentQuestionId;
            EnsureQuestionProgress(overview, activity);
            foreach (var removedQuestionId in previousAnswered.Except(currentAnswered))
            {
                AdjustAnsweredCount(overview, removedQuestionId, -1);
            }

            foreach (var addedQuestionId in currentAnswered.Except(previousAnswered))
            {
                AdjustAnsweredCount(overview, addedQuestionId, 1);
            }

            if (previousCurrentQuestion != state.CurrentQuestionId ||
                CountsAsCurrent(student.Status) != CountsAsCurrent(state.Status))
            {
                if (previousCurrentQuestion is { } previousQuestionId &&
                    CountsAsCurrent(student.Status))
                {
                    AdjustCurrentCount(overview, previousQuestionId, -1);
                }

                if (state.CurrentQuestionId is { } nextCurrentQuestionId &&
                    CountsAsCurrent(state.Status))
                {
                    AdjustCurrentCount(overview, nextCurrentQuestionId, 1);
                }
            }

            student.AttemptId = state.AttemptId;
            student.AttemptNumber = state.AttemptNumber;
            student.CurrentQuestionId = state.CurrentQuestionId;
            student.AnsweredCount = state.AnsweredCount;
            student.TotalQuestions = totalQuestions;
            student.Progress = progress;
            student.Status = state.Status;
            student.LastActivityAt = state.LastActivityAt;
            student.HelpRequested = state.HelpRequested;
            student.Version = state.Version;
            student.AnsweredQuestionIds = currentAnswered.Order().ToArray();
            overview.Version = Math.Max(overview.Version, state.Version);
            overview.UpdatedAt = DateTimeOffset.UtcNow;
            overviewChanged = true;
        }

        if (overviewChanged)
        {
            session.Store(overview);
        }
        await session.SaveChangesAsync(cancellationToken);

        if (!overviewChanged && !detailNeedsUpdate)
        {
            return;
        }

        ActivityOverviewDelta? overviewDelta = null;
        if (overviewChanged)
        {
            overviewDelta = new ActivityOverviewDelta(state.StudentId, state.AttemptId,
                state.CurrentQuestionId, state.AnsweredCount, totalQuestions, progress, state.Status,
                state.LastActivityAt, state.Version, state.AttemptNumber,
                overview.QuestionProgress.OrderBy(value => value.QuestionId)
                    .Select(value => new ActivityQuestionProgressDelta(value.QuestionId,
                        value.AnsweredStudentCount, value.CurrentStudentCount)).ToArray());
        }

        ActivityDetailDelta? detailDelta = null;
        if (detailNeedsUpdate)
        {
            detailDelta = new ActivityDetailDelta(state.AttemptId, state.Version, message.ChangeType,
                message.QuestionId, message.AnswerValue, state.CurrentQuestionId, state.AnsweredCount,
                state.Status, state.LastActivityAt, state.HelpRequested);
        }

        await notifier.NotifyAsync(message.OrganizationId, state.LearningClassId, state.ActivityId,
            state.AttemptId, overviewDelta, detailDelta, cancellationToken);
    }

    private static void EnsureQuestionProgress(ClassOverviewView overview,
        AssessmentActivity activity)
    {
        overview.QuestionProgress ??= [];
        var known = overview.QuestionProgress.Select(value => value.QuestionId)
            .ToHashSet();
        foreach (var question in activity.Questions)
        {
            if (!known.Contains(question.Id))
            {
                overview.QuestionProgress.Add(new ClassOverviewQuestionProgress
                {
                    QuestionId = question.Id
                });
            }
        }
    }

    private static void AdjustAnsweredCount(ClassOverviewView overview, Guid questionId,
        int delta)
    {
        var progress = overview.QuestionProgress.FirstOrDefault(value => value.QuestionId == questionId);
        if (progress is not null)
        {
            progress.AnsweredStudentCount = Math.Max(0, progress.AnsweredStudentCount + delta);
        }
    }

    private static void AdjustCurrentCount(ClassOverviewView overview, Guid questionId, int delta)
    {
        var progress = overview.QuestionProgress.FirstOrDefault(value => value.QuestionId == questionId);
        if (progress is not null)
        {
            progress.CurrentStudentCount = Math.Max(0, progress.CurrentStudentCount + delta);
        }
    }

    private static bool CountsAsCurrent(StudentActivityStatus? status) =>
        status is StudentActivityStatus.InProgress or StudentActivityStatus.Idle or
            StudentActivityStatus.Paused;
}
