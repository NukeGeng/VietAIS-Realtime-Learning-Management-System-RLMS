using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using Marten;
using Wolverine;

namespace LearningPlatform.Reporting.Domain;

public static class ReportingProjectionHandler
{
    public static async Task Handle(EnrollmentChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);
        if (message.Status == EnrollmentStatus.Active)
        {
            await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
                message.StudentId, cancellationToken);
        }
    }

    public static Task Handle(AssessmentActivityChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken) =>
        RebuildClassAndActiveStudentsAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);

    public static Task Handle(EssayAssignmentChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken) =>
        RebuildClassAndActiveStudentsAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);

    public static async Task Handle(SubmissionChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);
        await using var query = store.QuerySession(message.OrganizationId.ToString("D"));
        if (await query.Query<Enrollment>().AnyAsync(value =>
                value.LearningClassId == message.LearningClassId &&
                value.StudentId == message.StudentId && value.Status == EnrollmentStatus.Active,
                cancellationToken))
        {
            await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
                message.StudentId, cancellationToken);
        }
    }

    public static async Task Handle(GradeEntryPublishedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);
        await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
            message.StudentId, cancellationToken);
    }

    public static async Task Handle(GradeEntryVisibilityChangedForReporting message,
        IDocumentStore store, CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);
        await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
            message.StudentId, cancellationToken);
    }

    public static async Task Handle(GradeItemChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await RebuildClassAndActiveStudentsAsync(store, message.OrganizationId,
            message.LearningClassId, cancellationToken);
    }

    private static async Task RebuildClassAndActiveStudentsAsync(IDocumentStore store,
        Guid organizationId, Guid classId, CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, organizationId, classId, cancellationToken);
        await using var query = store.QuerySession(organizationId.ToString("D"));
        var studentIds = await query.Query<Enrollment>()
            .Where(value => value.LearningClassId == classId &&
                value.Status == EnrollmentStatus.Active)
            .Select(value => value.StudentId)
            .ToListAsync(cancellationToken);
        foreach (var studentId in studentIds.Distinct())
        {
            await RebuildStudentAsync(store, organizationId, classId,
                studentId, cancellationToken);
        }
    }

    public static async Task Handle(LessonProgressChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
            message.StudentId, cancellationToken);
    }

    public static Task Handle(StudentProgressChangedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken) =>
        RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
            message.StudentId, cancellationToken);

    public static async Task Handle(LiveSessionEndedForReporting message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        var classId = await FindClassIdAsync(store, message, cancellationToken);
        await RebuildClassAsync(store, message.OrganizationId, classId, cancellationToken);
        foreach (var studentId in message.StudentIds.Distinct())
        {
            await RebuildStudentAsync(store, message.OrganizationId, classId, studentId,
                cancellationToken);
        }
    }

    public static async Task Handle(AttendanceRecordChangedForReporting message,
        IDocumentStore store, CancellationToken cancellationToken)
    {
        await RebuildClassAsync(store, message.OrganizationId, message.LearningClassId,
            cancellationToken);
        await RebuildStudentAsync(store, message.OrganizationId, message.LearningClassId,
            message.StudentId, cancellationToken);
    }

    private static async Task<Guid> FindClassIdAsync(IDocumentStore store,
        LiveSessionEndedForReporting message,
        CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession(message.OrganizationId.ToString("D"));
        var liveSession = await session.LoadAsync<LiveSession>(message.LiveSessionId, cancellationToken);
        return liveSession?.LearningClassId ?? Guid.Empty;
    }

    public static async Task RebuildClassAsync(IDocumentStore store, Guid organizationId,
        Guid classId, CancellationToken cancellationToken)
    {
        if (classId == Guid.Empty)
        {
            return;
        }

        await using var session = store.LightweightSession(organizationId.ToString("D"));
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return;
        }

        var enrollments = await session.Query<Enrollment>()
            .Where(value => value.LearningClassId == classId)
            .ToListAsync(cancellationToken);
        var activities = await session.Query<AssessmentActivity>()
            .Where(value => value.LearningClassId == classId &&
                value.Status != AssessmentActivityStatus.Draft)
            .ToListAsync(cancellationToken);
        var essays = await session.Query<EssayAssignment>()
            .Where(value => value.LearningClassId == classId &&
                value.Status != EssayAssignmentStatus.Draft)
            .ToListAsync(cancellationToken);
        var activityIds = activities.Select(value => value.Id).ToArray();
        var essayAssignmentIds = essays.Select(value => value.Id).ToArray();
        var gradeItems = await session.Query<GradeItem>()
            .Where(value => value.LearningClassId == classId &&
                (activityIds.Contains(value.ActivityId) ||
                 value.EssayAssignmentId != null && essayAssignmentIds.Contains(value.EssayAssignmentId.Value)))
            .ToListAsync(cancellationToken);
        var gradeItemIds = gradeItems.Select(value => value.Id).ToArray();
        var submissions = activityIds.Length == 0
            ? []
            : await session.Query<Submission>()
                .Where(value => activityIds.Contains(value.ActivityId))
                .ToListAsync(cancellationToken);
        var essaySubmissions = essayAssignmentIds.Length == 0
            ? []
            : await session.Query<EssaySubmission>()
                .Where(value => essayAssignmentIds.Contains(value.EssayAssignmentId) &&
                    value.Status != EssaySubmissionStatus.Rejected)
                .ToListAsync(cancellationToken);
        var gradeEntries = gradeItemIds.Length == 0
            ? []
            : await session.Query<GradeEntry>()
                .Where(value => gradeItemIds.Contains(value.GradeItemId) &&
                    value.Status == GradeEntryStatus.Published)
                .ToListAsync(cancellationToken);
        // A retake creates a separate published GradeEntry. Reports follow the
        // same latest-attempt semantics as the gradebook so one student/item
        // does not receive extra weight merely for retaking an activity.
        gradeEntries = gradeEntries
            .GroupBy(value => (value.GradeItemId, value.StudentId))
            .Select(group => group.OrderByDescending(value => value.AttemptNumber)
                .ThenByDescending(value => value.Version)
                .First())
            .ToList();
        var sessions = await session.Query<LiveSession>()
            .Where(value => value.LearningClassId == classId &&
                value.Status == LiveSessionStatus.Ended)
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(value => value.Id).ToArray();
        var attendance = sessionIds.Length == 0
            ? []
            : await session.Query<AttendanceRecord>()
                .Where(value => sessionIds.Contains(value.LiveSessionId))
                .ToListAsync(cancellationToken);
        var overviews = await session.Query<ClassOverviewView>()
            .Where(value => value.LearningClassId == classId)
            .ToListAsync(cancellationToken);
        var overviewStudents = overviews.SelectMany(value => value.Students ?? [])
            .GroupBy(value => value.StudentId)
            .Select(group => group.OrderByDescending(value => value.Version).First())
            .ToArray();
        var attended = attendance.Count(value => value.Status is AttendanceStatus.Present or
            AttendanceStatus.Late or AttendanceStatus.Excused);
        var attendanceDenominator = sessions.Count * enrollments.Count(value =>
            value.Status == EnrollmentStatus.Active);
        var gradeItemById = gradeItems.ToDictionary(value => value.Id);
        var weightedGrades = gradeEntries
            .Where(value => gradeItemById.GetValueOrDefault(value.GradeItemId)?.Weight > 0m)
            .Select(value =>
            {
                var weight = gradeItemById[value.GradeItemId].Weight;
                var normalized = value.MaxScore == 0m ? 0m : value.Score / value.MaxScore;
                return (Weight: weight, Score: normalized * weight);
            })
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var snapshot = await session.LoadAsync<ClassReportSnapshot>(
            ReportingConstants.ClassSnapshotId(classId), cancellationToken) ?? new ClassReportSnapshot
            {
                Id = ReportingConstants.ClassSnapshotId(classId),
                OrganizationId = organizationId,
                LearningClassId = classId
            };
        snapshot.CourseId = learningClass.CourseId;
        snapshot.EnrollmentCount = enrollments.Count;
        snapshot.ActiveEnrollmentCount = enrollments.Count(value => value.Status == EnrollmentStatus.Active);
        snapshot.CompletedEnrollmentCount = enrollments.Count(value => value.Status == EnrollmentStatus.Completed);
        snapshot.ActivityCount = activities.Count + essays.Count;
        snapshot.PublishedActivityCount = activities.Count(value =>
            value.Status == AssessmentActivityStatus.Published) + essays.Count(value =>
            value.Status == EssayAssignmentStatus.Published);
        snapshot.SubmissionCount = submissions.Count + essaySubmissions.Count;
        snapshot.PublishedGradeCount = gradeEntries.Count;
        snapshot.AverageScorePercent = weightedGrades.Length == 0 || weightedGrades.Sum(value => value.Weight) == 0m
            ? 0m
            : decimal.Round(weightedGrades.Sum(value => value.Score) * 100m /
                weightedGrades.Sum(value => value.Weight), 2);
        snapshot.AttendanceSessionCount = sessions.Count;
        snapshot.AttendancePresentCount = attendance.Count(value => value.Status == AttendanceStatus.Present);
        snapshot.AttendanceLateCount = attendance.Count(value => value.Status == AttendanceStatus.Late);
        snapshot.AttendanceAbsentCount = attendance.Count(value => value.Status == AttendanceStatus.Absent);
        snapshot.AttendanceExcusedCount = attendance.Count(value => value.Status == AttendanceStatus.Excused);
        snapshot.AttendancePercentage = attendanceDenominator == 0
            ? 0m
            : decimal.Round(attended * 100m / attendanceDenominator, 2);
        snapshot.OverviewStudentCount = overviewStudents.Length;
        snapshot.OverviewInProgressCount = overviewStudents.Count(value =>
            value.Status == StudentActivityStatus.InProgress);
        snapshot.OverviewSubmittedCount = overviewStudents.Count(value =>
            value.Status == StudentActivityStatus.Submitted);
        snapshot.OverviewHelpRequestedCount = overviewStudents.Count(value => value.HelpRequested);
        snapshot.Version++;
        snapshot.UpdatedAt = now;
        session.Store(snapshot);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task RebuildStudentAsync(IDocumentStore store, Guid organizationId,
        Guid classId, Guid studentId, CancellationToken cancellationToken)
    {
        if (classId == Guid.Empty || studentId == Guid.Empty)
        {
            return;
        }

        await using var session = store.LightweightSession(organizationId.ToString("D"));
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return;
        }

        var activities = await session.Query<AssessmentActivity>()
            .Where(value => value.LearningClassId == classId &&
                value.Status != AssessmentActivityStatus.Draft)
            .ToListAsync(cancellationToken);
        var essays = await session.Query<EssayAssignment>()
            .Where(value => value.LearningClassId == classId &&
                value.Status != EssayAssignmentStatus.Draft)
            .ToListAsync(cancellationToken);
        var activityIds = activities.Select(value => value.Id)
            .Concat(essays.Select(value => value.Id))
            .ToArray();
        var essayAssignmentIds = essays.Select(value => value.Id).ToArray();
        var activityById = activities.ToDictionary(value => value.Id);
        var gradeItems = await session.Query<GradeItem>()
            .Where(value => value.LearningClassId == classId &&
                (activityIds.Contains(value.ActivityId) ||
                 value.EssayAssignmentId != null && essayAssignmentIds.Contains(value.EssayAssignmentId.Value)))
            .ToListAsync(cancellationToken);
        var gradeItemById = gradeItems.ToDictionary(value => value.Id);
        var gradeItemIds = gradeItems.Select(value => value.Id).ToArray();
        var grades = gradeItemIds.Length == 0
            ? []
            : await session.Query<GradeEntry>()
                .Where(value => gradeItemIds.Contains(value.GradeItemId) && value.StudentId == studentId &&
                    value.Status == GradeEntryStatus.Published)
                .ToListAsync(cancellationToken);
        grades = grades
            .GroupBy(value => value.GradeItemId)
            .Select(group => group.OrderByDescending(value => value.AttemptNumber)
                .ThenByDescending(value => value.Version)
                .First())
            .ToList();
        var now = DateTimeOffset.UtcNow;
        grades = grades.Where(grade => !activityById.TryGetValue(grade.ActivityId,
                out var activity) || AssessmentRules.IsResultsReleased(activity, now))
            .ToList();
        var progress = await session.Query<StudentProgressRecord>()
            .Where(value => value.LearningClassId == classId && value.StudentId == studentId &&
                activityIds.Contains(value.ActivityId))
            .ToListAsync(cancellationToken);
        // A published grade can exist before its configured release time. Keep
        // the student's report consistent with the student-facing gradebook and
        // progress endpoints until that result is released.
        progress = progress.Where(value => !activityById.TryGetValue(value.ActivityId,
                out var activity) || AssessmentRules.IsResultsReleased(activity, now))
            .ToList();
        var visibleActivityIds = grades.Select(EffectiveActivityId)
            .Where(value => value != Guid.Empty)
            .ToHashSet();
        progress = progress.Where(value => visibleActivityIds.Contains(value.ActivityId)).ToList();
        var lessons = await session.Query<Lesson>()
            .Where(value => value.CourseId == learningClass.CourseId &&
                value.Status == AcademicContentStatus.Published)
            .ToListAsync(cancellationToken);
        var lessonIds = lessons.Select(value => value.Id).ToArray();
        var lessonProgress = lessonIds.Length == 0
            ? []
            : await session.Query<StudentLessonProgressRecord>()
                .Where(value => value.LearningClassId == classId && value.StudentId == studentId &&
                    lessonIds.Contains(value.LessonId))
                .ToListAsync(cancellationToken);
        var sessions = await session.Query<LiveSession>()
            .Where(value => value.LearningClassId == classId && value.Status == LiveSessionStatus.Ended)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        var attendance = sessions.Count == 0
            ? []
            : await session.Query<AttendanceRecord>()
                .Where(value => sessions.Contains(value.LiveSessionId) && value.StudentId == studentId)
                .ToListAsync(cancellationToken);
        var attended = attendance.Count(value => value.Status is AttendanceStatus.Present or
            AttendanceStatus.Late or AttendanceStatus.Excused);
        var weightedGrades = grades
            .Where(value => gradeItemById.GetValueOrDefault(value.GradeItemId)?.Weight > 0m)
            .Select(value =>
            {
                var weight = gradeItemById[value.GradeItemId].Weight;
                var normalized = value.MaxScore == 0m ? 0m : value.Score / value.MaxScore;
                return (Weight: weight, Score: normalized * weight);
            })
            .ToArray();
        var snapshot = await session.LoadAsync<StudentReportSnapshot>(
            ReportingConstants.StudentSnapshotId(classId, studentId), cancellationToken) ??
            new StudentReportSnapshot
            {
                Id = ReportingConstants.StudentSnapshotId(classId, studentId),
                OrganizationId = organizationId,
                LearningClassId = classId,
                StudentId = studentId
            };
        snapshot.PublishedGradeCount = grades.Count;
        snapshot.AverageScorePercent = weightedGrades.Length == 0 || weightedGrades.Sum(value => value.Weight) == 0m
            ? 0m
            : decimal.Round(weightedGrades.Sum(value => value.Score) * 100m /
                weightedGrades.Sum(value => value.Weight), 2);
        snapshot.ActivityCount = activityIds.Length;
        snapshot.CompletedActivityCount = progress.Count(value => value.Status == LearningProgressStatus.Completed);
        snapshot.ActivityCompletionPercent = activityIds.Length == 0
            ? 0m
            : decimal.Round(snapshot.CompletedActivityCount * 100m / activityIds.Length, 2);
        snapshot.LessonCount = lessons.Count;
        snapshot.CompletedLessonCount = lessonProgress.Count(value => value.Status == LessonProgressStatus.Completed);
        snapshot.LessonCompletionPercent = lessons.Count == 0
            ? 0m
            : decimal.Round(snapshot.CompletedLessonCount * 100m / lessons.Count, 2);
        snapshot.AttendanceSessionCount = sessions.Count;
        snapshot.AttendancePresentCount = attendance.Count(value => value.Status == AttendanceStatus.Present);
        snapshot.AttendanceLateCount = attendance.Count(value => value.Status == AttendanceStatus.Late);
        snapshot.AttendanceAbsentCount = attendance.Count(value => value.Status == AttendanceStatus.Absent);
        snapshot.AttendanceExcusedCount = attendance.Count(value => value.Status == AttendanceStatus.Excused);
        snapshot.AttendancePercentage = sessions.Count == 0
            ? 0m
            : decimal.Round(attended * 100m / sessions.Count, 2);
        snapshot.Version++;
        snapshot.UpdatedAt = now;
        session.Store(snapshot);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static Guid EffectiveActivityId(GradeEntry grade) =>
        grade.EssayAssignmentId ?? grade.ActivityId;
}
