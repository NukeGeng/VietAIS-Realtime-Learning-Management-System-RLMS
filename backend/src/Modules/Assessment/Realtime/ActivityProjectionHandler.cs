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
        if (existingDetail is not null && existingDetail.Version >= state.Version)
        {
            return;
        }

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
            Status = state.Status,
            Answers = new Dictionary<Guid, string>(state.Answers),
            AnsweredCount = state.AnsweredCount,
            TotalQuestions = totalQuestions,
            Progress = progress,
            StartedAt = state.StartedAt,
            LastActivityAt = state.LastActivityAt,
            SubmittedAt = state.SubmittedAt,
            Version = state.Version
        };
        session.Store(detail);

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
        var student = overview.Students.FirstOrDefault(item => item.StudentId == state.StudentId);
        var overviewChanged = false;
        if (student is null)
        {
            student = new ClassOverviewStudent { StudentId = state.StudentId };
            overview.Students.Add(student);
        }
        if (student.Version < state.Version || student.AttemptId == Guid.Empty)
        {
            student.AttemptId = state.AttemptId;
            student.CurrentQuestionId = state.CurrentQuestionId;
            student.AnsweredCount = state.AnsweredCount;
            student.TotalQuestions = totalQuestions;
            student.Progress = progress;
            student.Status = state.Status;
            student.LastActivityAt = state.LastActivityAt;
            student.Version = state.Version;
            overview.Version = Math.Max(overview.Version, state.Version);
            overview.UpdatedAt = DateTimeOffset.UtcNow;
            overviewChanged = true;
        }

        if (overviewChanged)
        {
            session.Store(overview);
        }
        await session.SaveChangesAsync(cancellationToken);

        if (!overviewChanged)
        {
            return;
        }

        var overviewDelta = new ActivityOverviewDelta(state.StudentId, state.AttemptId,
            state.CurrentQuestionId, state.AnsweredCount, totalQuestions, progress, state.Status,
            state.LastActivityAt, state.Version);
        var questionId = message.QuestionId;
        var detailDelta = new ActivityDetailDelta(state.AttemptId, state.Version, message.ChangeType,
            questionId, message.AnswerValue, state.CurrentQuestionId, state.AnsweredCount,
            state.Status, state.LastActivityAt);
        await notifier.NotifyAsync(message.OrganizationId, state.LearningClassId, state.ActivityId,
            state.AttemptId, overviewDelta, detailDelta, cancellationToken);
    }
}
