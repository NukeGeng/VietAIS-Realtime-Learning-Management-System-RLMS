using System.Net;
using System.Net.Http.Json;
using System.Text;
using LearningPlatform.Academics.Api;
using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Communication.Api;
using LearningPlatform.Learning.Api;
using LearningPlatform.Reporting.Api;
using LearningPlatform.Reporting.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace LearningPlatform.IntegrationTests;

public sealed class AssessmentActivityTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Teacher_can_publish_immutable_activity_and_student_can_save_and_submit_attempt()
    {
        var setup = await CreateActivitySetupAsync();
        using var detailTeacher = await CreateAssignedTeacherAsync(setup);

        using var studentActivity = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}");
        Assert.Equal(HttpStatusCode.OK, studentActivity.StatusCode);
        var visibleActivity = await studentActivity.Content.ReadFromJsonAsync<AssessmentActivityResponse>();
        Assert.NotNull(visibleActivity);
        Assert.Equal(AssessmentActivityStatus.Published, visibleActivity!.Status);
        Assert.Single(visibleActivity.Questions);
        Assert.Equal("What is two plus two?", visibleActivity.Questions[0].Prompt);

        using var start = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<StudentActivityResponse>();
        Assert.NotNull(started);
        Assert.Equal(StudentActivityStatus.InProgress, started!.Status);

        var questionId = setup.Activity.Questions[0].Id;
        using var open = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/questions/{questionId:D}/open",
            new { });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var opened = await open.Content.ReadFromJsonAsync<StudentActivityResponse>();
        Assert.Equal(questionId, opened!.CurrentQuestionId);

        using var save = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("B"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<StudentActivityResponse>();
        Assert.Equal("B", saved!.Answers[questionId]);
        Assert.Equal(1, saved.AnsweredCount);

        using var changed = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("A"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var changedAttempt = await changed.Content.ReadFromJsonAsync<StudentActivityResponse>();
        Assert.Equal("A", changedAttempt!.Answers[questionId]);

        var overviewDelta = new TaskCompletionSource<ActivityOverviewDelta>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ActivityOverviewDelta? receivedDelta = null;
        await using (var connection = new HubConnectionBuilder()
                         .WithUrl("https://localhost/hubs/assessment", options =>
                         {
                             options.Transports = HttpTransportType.LongPolling;
                             options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                             options.Headers["Cookie"] = detailTeacher.DefaultRequestHeaders
                                 .GetValues("Cookie").Single();
                         }).Build())
        {
            connection.On<ActivityOverviewDelta>("activity-overview-delta",
                delta =>
                {
                    if (delta.Version >= 5)
                    {
                        overviewDelta.TrySetResult(delta);
                    }
                });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.StartAsync(timeout.Token);
            await connection.InvokeAsync("SubscribeOverview", setup.ClassId, setup.Activity.Id,
                timeout.Token);
            await connection.InvokeAsync("SubscribeStudentDetail", started.Id, timeout.Token);

            using var realtimeSave = await PutWithCsrfAsync(setup.Student,
                $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
                new SaveAnswerRequest("B"));
            Assert.Equal(HttpStatusCode.OK, realtimeSave.StatusCode);
            receivedDelta = await overviewDelta.Task.WaitAsync(timeout.Token);
            Assert.Equal(setup.StudentId, receivedDelta.StudentId);
            Assert.Equal(5, receivedDelta.Version);
            Assert.Equal("B", (await realtimeSave.Content.ReadFromJsonAsync<StudentActivityResponse>())!
                .Answers[questionId]);

            await connection.InvokeAsync("UnsubscribeStudentDetail", started.Id, timeout.Token);
            await connection.InvokeAsync("UnsubscribeOverview", setup.ClassId, setup.Activity.Id,
                timeout.Token);
            await connection.StopAsync(timeout.Token);
        }

        ClassOverviewResponse? overview = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (overview is null || overview.Students.Single(student =>
                       student.StudentId == setup.StudentId).Version < receivedDelta.Version)
            {
                using var response = await setup.Teacher.GetAsync(
                    $"/api/assessment/classes/{setup.ClassId:D}/activities/{setup.Activity.Id:D}/overview",
                    timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                overview = await response.Content.ReadFromJsonAsync<ClassOverviewResponse>(timeout.Token);
                if (overview!.Students.Single(student => student.StudentId == setup.StudentId).Version >= 2)
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }
        var overviewStudent = Assert.Single(overview!.Students,
            student => student.StudentId == setup.StudentId);
        Assert.Equal(5, overviewStudent.Version);
        Assert.Equal(1, overviewStudent.AnsweredCount);
        Assert.Equal(100m, overviewStudent.Progress);
        var questionProgress = Assert.Single(overview.QuestionProgress);
        Assert.Equal(questionId, questionProgress.QuestionId);
        Assert.Equal(1, questionProgress.AnsweredStudentCount);
        Assert.DoesNotContain("Answers", await (await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/activities/{setup.Activity.Id:D}/overview"))
            .Content.ReadAsStringAsync());

        using var adminDetailResponse = await setup.Teacher.GetAsync(
            $"/api/assessment/attempts/{started.Id:D}/detail");
        Assert.Equal(HttpStatusCode.Forbidden, adminDetailResponse.StatusCode);

        using var detailResponse = await detailTeacher.GetAsync(
            $"/api/assessment/attempts/{started.Id:D}/detail");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<StudentAttemptDetailResponse>();
        Assert.Equal("B", detail!.Answers[questionId]);

        using var submit = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/submit", new { });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var submission = await submit.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.NotNull(submission);
        Assert.Equal(setup.Activity.Id, submission!.ActivityId);
        Assert.Contains(submission.Answers, answer => answer.QuestionId == questionId && answer.Value == "B");

        using var gradeBeforePublish = await setup.Teacher.GetAsync(
            $"/api/assessment/submissions/{submission.Id:D}/grade");
        Assert.Equal(HttpStatusCode.OK, gradeBeforePublish.StatusCode);
        var automaticGrade = await gradeBeforePublish.Content.ReadFromJsonAsync<GradeEntryResponse>();
        Assert.NotNull(automaticGrade);
        Assert.Equal(5m, automaticGrade!.Score);
        Assert.True(automaticGrade.IsComplete);
        Assert.Equal(GradeEntryStatus.Draft, automaticGrade.Status);
        Assert.Equal(GradeSource.Automatic, automaticGrade.Source);

        using var hiddenResult = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}/result");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResult.StatusCode);
        using var studentGradeAttempt = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/grade-entries/{automaticGrade.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.Forbidden, studentGradeAttempt.StatusCode);

        using var publishGrade = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{automaticGrade.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publishGrade.StatusCode);
        var publishedGrade = await publishGrade.Content.ReadFromJsonAsync<GradeEntryResponse>();
        Assert.Equal(GradeEntryStatus.Published, publishedGrade!.Status);

        NotificationResponse[] resultNotifications = [];
        using (var notificationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (!resultNotifications.Any(notification =>
                       notification.Type == "result-published" &&
                       notification.SourceId == automaticGrade.Id))
            {
                resultNotifications = await setup.Student.GetFromJsonAsync<NotificationResponse[]>(
                    "/api/communication/notifications?unreadOnly=true",
                    notificationTimeout.Token) ?? [];
                if (resultNotifications.Any(notification =>
                        notification.Type == "result-published" &&
                        notification.SourceId == automaticGrade.Id))
                {
                    break;
                }

                await Task.Delay(100, notificationTimeout.Token);
            }
        }
        Assert.Contains(resultNotifications, notification =>
            notification.Type == "result-published" && notification.SourceId == automaticGrade.Id);

        var organization = await fixture.EnsureBootstrappedAsync();
        ClassReportSnapshot? projectedReport = null;
        using (var projectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (projectedReport is null || projectedReport.PublishedGradeCount == 0)
            {
                await using var projectionSession = fixture.Services.GetRequiredService<IDocumentStore>()
                    .QuerySession(organization.OrganizationId.ToString("D"));
                projectedReport = await projectionSession.LoadAsync<ClassReportSnapshot>(
                    ReportingConstants.ClassSnapshotId(setup.ClassId), projectionTimeout.Token);
                if (projectedReport?.PublishedGradeCount > 0)
                {
                    break;
                }

                await Task.Delay(100, projectionTimeout.Token);
            }
        }
        Assert.Equal(1, projectedReport!.PublishedGradeCount);

        ClassReportResponse? classReport = null;
        using (var reportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (classReport is null || classReport.PublishedGradeCount == 0)
            {
                using var reportResponse = await setup.Teacher.GetAsync(
                    $"/api/reporting/classes/{setup.ClassId:D}/summary", reportTimeout.Token);
                Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
                classReport = await reportResponse.Content.ReadFromJsonAsync<ClassReportResponse>(
                    reportTimeout.Token);
                if (classReport!.PublishedGradeCount > 0)
                {
                    break;
                }

                await Task.Delay(100, reportTimeout.Token);
            }
        }
        Assert.Equal(1, classReport!.PublishedGradeCount);
        Assert.Equal(1, classReport.SubmissionCount);

        using var activityReport = await setup.Teacher.GetAsync(
            $"/api/reporting/activities/{setup.Activity.Id:D}/summary");
        Assert.Equal(HttpStatusCode.OK, activityReport.StatusCode);
        var activitySummary = (await activityReport.Content.ReadFromJsonAsync<ActivityReportResponse>())!;
        Assert.Equal(1, activitySummary.PublishedGradeCount);
        Assert.Equal(1, activitySummary.Questions[0].CorrectAnswerCount);

        using var studentReport = await setup.Student.GetAsync(
            $"/api/reporting/classes/{setup.ClassId:D}/students/me");
        Assert.Equal(HttpStatusCode.OK, studentReport.StatusCode);

        using var teacherGradebook = await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/gradebook");
        Assert.Equal(HttpStatusCode.OK, teacherGradebook.StatusCode);
        var classGradebook = await teacherGradebook.Content.ReadFromJsonAsync<GradebookResponse>();
        var teacherCell = Assert.Single(Assert.Single(classGradebook!.Students).Entries);
        Assert.Equal(GradeEntryStatus.Published, teacherCell.Status);

        using var studentResult = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}/result");
        Assert.Equal(HttpStatusCode.OK, studentResult.StatusCode);
        var result = await studentResult.Content.ReadFromJsonAsync<StudentResultResponse>();
        Assert.Equal(5m, result!.Score);

        using var studentGradebook = await setup.Student.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/gradebook/me");
        Assert.Equal(HttpStatusCode.OK, studentGradebook.StatusCode);
        var ownGradebook = await studentGradebook.Content.ReadFromJsonAsync<GradebookResponse>();
        var ownCell = Assert.Single(Assert.Single(ownGradebook!.Students).Entries);
        Assert.Equal(5m, ownCell.Score);

        LearningProgressResponse? progress = null;
        using (var progressTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (progress is null || !progress.Entries.Any(entry =>
                       entry.ActivityId == setup.Activity.Id && entry.Completion == 100m))
            {
                using var studentProgress = await setup.Student.GetAsync(
                    $"/api/learning/classes/{setup.ClassId:D}/progress/me", progressTimeout.Token);
                Assert.Equal(HttpStatusCode.OK, studentProgress.StatusCode);
                progress = await studentProgress.Content.ReadFromJsonAsync<LearningProgressResponse>(
                    progressTimeout.Token);
                if (progress!.Entries.Any(entry => entry.ActivityId == setup.Activity.Id &&
                        entry.Completion == 100m))
                {
                    break;
                }

                await Task.Delay(100, progressTimeout.Token);
            }
        }

        using var attempt = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt");
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        var completed = await attempt.Content.ReadFromJsonAsync<StudentActivityResponse>();
        Assert.Equal(StudentActivityStatus.Submitted, completed!.Status);

        using var addAfterPublish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/activities/{setup.Activity.Id:D}/questions", new
            {
                QuestionVersionId = setup.QuestionVersion.Id,
                Order = 1,
                Points = 1m
            });
        Assert.Equal(HttpStatusCode.Conflict, addAfterPublish.StatusCode);

        using var newVersion = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/question-bank/{setup.QuestionItemId:D}/versions", new
            {
                Prompt = "Changed prompt must not rewrite published activity.",
                Type = (int)QuestionType.SingleChoice,
                Options = new[]
                {
                    new { Key = "A", Text = "3" },
                    new { Key = "B", Text = "4" }
                },
                CorrectAnswers = new[] { "B" }
            });
        Assert.Equal(HttpStatusCode.Created, newVersion.StatusCode);

        using var activityAfterVersion = await setup.Teacher.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}");
        var unchanged = await activityAfterVersion.Content.ReadFromJsonAsync<AssessmentActivityResponse>();
        Assert.Equal("What is two plus two?", unchanged!.Questions[0].Prompt);

        using var weightUpdate = await PutWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-items/{automaticGrade.GradeItemId:D}", new { Weight = 60m });
        Assert.Equal(HttpStatusCode.OK, weightUpdate.StatusCode);
        var updatedItem = await weightUpdate.Content.ReadFromJsonAsync<GradebookItemResponse>();
        Assert.Equal(60m, updatedItem!.Weight);
        using var adjustment = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{automaticGrade.Id:D}/adjust", new
            {
                Score = 4m,
                Feedback = "Adjusted after review",
                Reason = "Teacher review found a scoring correction."
            });
        Assert.Equal(HttpStatusCode.OK, adjustment.StatusCode);
        var adjusted = await adjustment.Content.ReadFromJsonAsync<GradeEntryResponse>();
        Assert.Equal(GradeEntryStatus.Draft, adjusted!.Status);
        Assert.Equal(4m, adjusted.Score);

        LearningProgressResponse? adjustedProgress = null;
        using (var adjustmentProgressTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (adjustedProgress is null || adjustedProgress.Entries.Any(entry =>
                       entry.ActivityId == setup.Activity.Id))
            {
                using var progressResponse = await setup.Student.GetAsync(
                    $"/api/learning/classes/{setup.ClassId:D}/progress/me",
                    adjustmentProgressTimeout.Token);
                progressResponse.EnsureSuccessStatusCode();
                adjustedProgress = await progressResponse.Content.ReadFromJsonAsync<LearningProgressResponse>(
                    adjustmentProgressTimeout.Token);
                if (adjustedProgress is not null && !adjustedProgress.Entries.Any(entry =>
                        entry.ActivityId == setup.Activity.Id))
                {
                    break;
                }

                await Task.Delay(100, adjustmentProgressTimeout.Token);
            }
        }
        Assert.DoesNotContain(adjustedProgress!.Entries, entry => entry.ActivityId == setup.Activity.Id);

        StudentReportResponse? adjustedReport = null;
        using (var adjustmentReportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (adjustedReport is null || adjustedReport.CompletedActivityCount != 0)
            {
                using var reportResponse = await setup.Student.GetAsync(
                    $"/api/reporting/classes/{setup.ClassId:D}/students/me",
                    adjustmentReportTimeout.Token);
                reportResponse.EnsureSuccessStatusCode();
                adjustedReport = await reportResponse.Content.ReadFromJsonAsync<StudentReportResponse>(
                    adjustmentReportTimeout.Token);
                if (adjustedReport?.CompletedActivityCount == 0)
                {
                    break;
                }

                await Task.Delay(100, adjustmentReportTimeout.Token);
            }
        }
        Assert.Equal(0, adjustedReport!.PublishedGradeCount);
        Assert.Equal(0, adjustedReport.CompletedActivityCount);

        using var adjustments = await setup.Teacher.GetAsync(
            $"/api/assessment/grade-entries/{automaticGrade.Id:D}/adjustments");
        Assert.Equal(HttpStatusCode.OK, adjustments.StatusCode);
        var audit = await adjustments.Content.ReadFromJsonAsync<GradeAdjustmentResponse[]>();
        var adjustmentRecord = Assert.Single(audit!);
        Assert.Equal(5m, adjustmentRecord.PreviousScore);
        Assert.Equal(4m, adjustmentRecord.NewScore);
        using var republish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{automaticGrade.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, republish.StatusCode);

        var otherOrganization = await fixture.CreateTestOrganizationAsync();
        using var otherTenant = fixture.Client();
        FoundationFixture.SetCookies(otherTenant, await fixture.LoginCookieAsync(otherOrganization.Email,
            "correct horse battery staple", otherOrganization.Code));
        using var crossTenant = await otherTenant.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Activity_pause_blocks_answer_and_student_cannot_manage_activity()
    {
        var setup = await CreateActivitySetupAsync();
        var questionId = setup.Activity.Questions[0].Id;

        using var denied = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/classes/{setup.ClassId:D}/activities", new
            {
                Title = "Student activity",
                Description = "",
                Kind = (int)AssessmentActivityKind.Assignment,
                OpensAt = (DateTimeOffset?)null,
                DueAt = (DateTimeOffset?)null,
                TimeLimit = (TimeSpan?)null
            });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var start = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var pause = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/activities/{setup.Activity.Id:D}/pause", new { });
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);

        using var blocked = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("B"));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using var resume = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/activities/{setup.Activity.Id:D}/resume", new { });
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
    }

    [Fact]
    public async Task Student_idle_and_help_state_are_visible_to_teacher_and_can_be_resolved()
    {
        var setup = await CreateActivitySetupAsync();
        using var start = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        start.EnsureSuccessStatusCode();
        var attempt = (await start.Content.ReadFromJsonAsync<StudentActivityResponse>())!;

        using var idle = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/idle", new { });
        Assert.Equal(HttpStatusCode.OK, idle.StatusCode);
        var idleAttempt = (await idle.Content.ReadFromJsonAsync<StudentActivityResponse>())!;
        Assert.Equal(StudentActivityStatus.Idle, idleAttempt.Status);

        using var resume = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/resume", new { });
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        Assert.Equal(StudentActivityStatus.InProgress,
            (await resume.Content.ReadFromJsonAsync<StudentActivityResponse>())!.Status);

        using var help = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/help", new { });
        Assert.Equal(HttpStatusCode.OK, help.StatusCode);
        var helpAttempt = (await help.Content.ReadFromJsonAsync<StudentActivityResponse>())!;
        Assert.True(helpAttempt.HelpRequested);
        Assert.NotNull(helpAttempt.HelpRequestedAt);

        ClassOverviewResponse? overview = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (overview is null || !Assert.Single(overview.Students).HelpRequested)
            {
                using var response = await setup.Teacher.GetAsync(
                    $"/api/assessment/classes/{setup.ClassId:D}/activities/{setup.Activity.Id:D}/overview",
                    timeout.Token);
                response.EnsureSuccessStatusCode();
                overview = await response.Content.ReadFromJsonAsync<ClassOverviewResponse>(timeout.Token);
                if (overview is not null && Assert.Single(overview.Students).HelpRequested)
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }

        using var helpQueue = await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/help-requests");
        Assert.Equal(HttpStatusCode.OK, helpQueue.StatusCode);
        var requests = await helpQueue.Content.ReadFromJsonAsync<HelpRequestResponse[]>();
        var request = Assert.Single(requests!);
        Assert.Equal(attempt.Id, request.AttemptId);
        Assert.Equal(setup.Activity.Id, request.ActivityId);
        Assert.Equal(HelpRequestStatus.Requested, request.HelpStatus);
        Assert.True(request.WaitDurationSeconds >= 0);

        using var acknowledged = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/attempts/{attempt.Id:D}/help/acknowledge", new { });
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var acknowledgedAttempt = (await acknowledged.Content
            .ReadFromJsonAsync<StudentActivityResponse>())!;
        Assert.True(acknowledgedAttempt.HelpRequested);
        Assert.NotNull(acknowledgedAttempt.HelpAcknowledgedAt);

        using var acknowledgedQueue = await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/help-requests");
        acknowledgedQueue.EnsureSuccessStatusCode();
        var acknowledgedRequest = Assert.Single(
            (await acknowledgedQueue.Content.ReadFromJsonAsync<HelpRequestResponse[]>())!);
        Assert.Equal(HelpRequestStatus.Acknowledged, acknowledgedRequest.HelpStatus);
        Assert.NotNull(acknowledgedRequest.AcknowledgedAt);

        using var resolved = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/attempts/{attempt.Id:D}/help/resolve", new { });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.False((await resolved.Content.ReadFromJsonAsync<StudentActivityResponse>())!.HelpRequested);

        using var resolvedQueue = await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/help-requests");
        resolvedQueue.EnsureSuccessStatusCode();
        Assert.Empty((await resolvedQueue.Content.ReadFromJsonAsync<HelpRequestResponse[]>())!);
    }

    [Fact]
    public async Task Teacher_must_manually_grade_subjective_submission_before_publishing()
    {
        var setup = await CreateActivitySetupAsync(QuestionType.LongText);
        var questionId = setup.Activity.Questions[0].Id;
        using var createRubric = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/activities/{setup.Activity.Id:D}/rubric", new
            {
                Criteria = new[]
                {
                    new
                    {
                        Id = Guid.Empty,
                        Name = "Reasoning",
                        Description = "Evidence and reasoning",
                        MaxPoints = 5m,
                        Order = 0
                    }
                }
            });
        createRubric.EnsureSuccessStatusCode();
        var rubric = (await createRubric.Content.ReadFromJsonAsync<RubricResponse>())!;
        var criterionId = Assert.Single(rubric.Criteria).Id;

        using var start = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        start.EnsureSuccessStatusCode();
        using var open = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/questions/{questionId:D}/open",
            new { });
        open.EnsureSuccessStatusCode();
        using var answer = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("A considered response"));
        answer.EnsureSuccessStatusCode();
        using var submit = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/submit", new { });
        submit.EnsureSuccessStatusCode();
        var submission = (await submit.Content.ReadFromJsonAsync<SubmissionResponse>())!;

        using var draftResponse = await setup.Teacher.GetAsync(
            $"/api/assessment/submissions/{submission.Id:D}/grade");
        var draft = (await draftResponse.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        Assert.False(draft.IsComplete);
        Assert.Equal(GradeEntryStatus.Draft, draft.Status);
        using var blockedPublish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{draft.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.Conflict, blockedPublish.StatusCode);

        using var grade = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/submissions/{submission.Id:D}/grade", new
            {
                Questions = new[]
                {
                    new
                    {
                        QuestionId = questionId,
                        Score = 4m,
                        Feedback = "Clear explanation",
                        RubricCriterionId = criterionId
                    }
                },
                Feedback = "Good work",
                RubricId = rubric.Id
            });
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        var manual = (await grade.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        Assert.Equal(GradeSource.Manual, manual.Source);
        Assert.True(manual.IsComplete);
        Assert.Equal(4m, manual.Score);

        using var publish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{manual.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var result = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}/result");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(4m, (await result.Content.ReadFromJsonAsync<StudentResultResponse>())!.Score);
    }

    [Fact]
    public async Task Teacher_can_publish_activity_directly_in_a_started_live_session()
    {
        var setup = await CreateActivitySetupAsync();
        using var create = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/classes/{setup.ClassId:D}/activities", new
            {
                Title = "In-class activity",
                Description = "Published from the live classroom",
                Kind = (int)AssessmentActivityKind.Exercise,
                OpensAt = (DateTimeOffset?)null,
                DueAt = (DateTimeOffset?)null,
                TimeLimit = (TimeSpan?)null
            });
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        using var addQuestion = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/activities/{draft.Id:D}/questions", new
            {
                QuestionVersionId = setup.QuestionVersion.Id,
                Order = 0,
                Points = 1m
            });
        addQuestion.EnsureSuccessStatusCode();

        using var createSession = await PostWithCsrfAsync(setup.Teacher,
            $"/api/classroom/classes/{setup.ClassId:D}/sessions", new
            {
                TeacherId = setup.TeacherId,
                ClassScheduleId = (Guid?)null
            });
        createSession.EnsureSuccessStatusCode();
        var liveSession = (await createSession.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
        using var start = await PostWithCsrfAsync(setup.Teacher,
            $"/api/classroom/sessions/{liveSession.Id:D}/start", new { });
        start.EnsureSuccessStatusCode();

        using var publish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/classroom/sessions/{liveSession.Id:D}/activities/{draft.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<AssessmentActivityResponse>();
        Assert.Equal(liveSession.Id, published!.LiveSessionId);
        Assert.Equal(AssessmentActivityStatus.Published, published.Status);
    }

    [Fact]
    public async Task Student_detail_switch_unsubscribes_previous_attempt_before_subscribing_next()
    {
        var setup = await CreateActivitySetupAsync(includeSecondStudent: true);
        using var detailTeacher = await CreateAssignedTeacherAsync(setup);
        var secondStudent = setup.SecondStudent!;

        using var startA = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        startA.EnsureSuccessStatusCode();
        var attemptA = (await startA.Content.ReadFromJsonAsync<StudentActivityResponse>())!;
        var questionId = setup.Activity.Questions[0].Id;

        using var startB = await PostWithCsrfAsync(secondStudent,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        startB.EnsureSuccessStatusCode();
        var attemptB = (await startB.Content.ReadFromJsonAsync<StudentActivityResponse>())!;

        var detailA = new TaskCompletionSource<ActivityDetailDelta>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var detailB = new TaskCompletionSource<ActivityDetailDelta>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/assessment", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                options.Headers["Cookie"] = detailTeacher.DefaultRequestHeaders
                    .GetValues("Cookie").Single();
            }).Build();
        connection.On<ActivityDetailDelta>("activity-detail-delta", delta =>
        {
            if (delta.AttemptId == attemptA.Id)
            {
                detailA.TrySetResult(delta);
            }

            if (delta.AttemptId == attemptB.Id && delta.Version >= 2)
            {
                detailB.TrySetResult(delta);
            }
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await connection.StartAsync(timeout.Token);
        await connection.InvokeAsync("SubscribeStudentDetail", attemptA.Id, timeout.Token);

        using var saveA = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("B"));
        saveA.EnsureSuccessStatusCode();
        Assert.Equal(attemptA.Id, (await detailA.Task.WaitAsync(timeout.Token)).AttemptId);

        await connection.InvokeAsync("UnsubscribeStudentDetail", attemptA.Id, timeout.Token);
        await connection.InvokeAsync("SubscribeStudentDetail", attemptB.Id, timeout.Token);

        using var changedA = await PutWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("A"));
        changedA.EnsureSuccessStatusCode();
        await Task.Delay(250, timeout.Token);
        Assert.False(detailB.Task.IsCompleted);

        using var saveB = await PutWithCsrfAsync(secondStudent,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/answers/{questionId:D}",
            new SaveAnswerRequest("B"));
        saveB.EnsureSuccessStatusCode();
        var receivedB = await detailB.Task.WaitAsync(timeout.Token);
        Assert.Equal(attemptB.Id, receivedB.AttemptId);
        Assert.Equal("B", receivedB.AnswerValue);

        await connection.StopAsync(timeout.Token);
    }

    [Fact]
    public async Task Exam_enforces_attempt_limit_and_creates_a_separate_attempt_for_a_retake()
    {
        var setup = await CreateActivitySetupAsync(activityKind: AssessmentActivityKind.Exam,
            examPolicy: new ExamPolicy { AttemptLimit = 2, AllowRetake = true, PassingScore = 60m });

        using var startOne = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        startOne.EnsureSuccessStatusCode();
        var first = (await startOne.Content.ReadFromJsonAsync<StudentActivityResponse>())!;
        Assert.Equal(1, first.AttemptNumber);
        using var submitOne = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/submit", new { });
        submitOne.EnsureSuccessStatusCode();

        using var startTwo = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        startTwo.EnsureSuccessStatusCode();
        var second = (await startTwo.Content.ReadFromJsonAsync<StudentActivityResponse>())!;
        Assert.Equal(2, second.AttemptNumber);
        Assert.NotEqual(first.Id, second.Id);

        ClassOverviewResponse? retakeOverview = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (retakeOverview is null || Assert.Single(retakeOverview.Students).AttemptNumber < 2)
            {
                using var response = await setup.Teacher.GetAsync(
                    $"/api/assessment/classes/{setup.ClassId:D}/activities/{setup.Activity.Id:D}/overview",
                    timeout.Token);
                response.EnsureSuccessStatusCode();
                retakeOverview = await response.Content.ReadFromJsonAsync<ClassOverviewResponse>(
                    timeout.Token);
                if (retakeOverview is not null && Assert.Single(retakeOverview.Students).AttemptNumber == 2)
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }
        Assert.Equal(2, Assert.Single(retakeOverview!.Students).AttemptNumber);

        using var submitTwo = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/submit", new { });
        submitTwo.EnsureSuccessStatusCode();

        using var startThree = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        Assert.Equal(HttpStatusCode.Conflict, startThree.StatusCode);
    }

    [Fact]
    public async Task Exam_results_are_hidden_until_release_time_in_student_reads()
    {
        var releaseAt = DateTimeOffset.UtcNow.AddSeconds(3);
        var setup = await CreateActivitySetupAsync(activityKind: AssessmentActivityKind.Exam,
            examPolicy: new ExamPolicy
            {
                AttemptLimit = 1,
                ResultsReleaseAt = releaseAt
            });

        using var start = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/start", new { });
        start.EnsureSuccessStatusCode();
        using var submit = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/activities/{setup.Activity.Id:D}/attempt/submit", new { });
        submit.EnsureSuccessStatusCode();
        var submission = (await submit.Content.ReadFromJsonAsync<SubmissionResponse>())!;

        using var draft = await setup.Teacher.GetAsync(
            $"/api/assessment/submissions/{submission.Id:D}/grade");
        var grade = (await draft.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        using var publish = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/grade-entries/{grade.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();

        using var hiddenResult = await setup.Student.GetAsync(
            $"/api/assessment/activities/{setup.Activity.Id:D}/result");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResult.StatusCode);

        using var hiddenGradebook = await setup.Student.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/gradebook/me");
        hiddenGradebook.EnsureSuccessStatusCode();
        var studentGradebook = (await hiddenGradebook.Content.ReadFromJsonAsync<GradebookResponse>())!;
        var hiddenCell = Assert.Single(Assert.Single(studentGradebook.Students).Entries);
        Assert.Null(hiddenCell.Score);

        using var hiddenProgress = await setup.Student.GetAsync(
            $"/api/learning/classes/{setup.ClassId:D}/progress/me");
        hiddenProgress.EnsureSuccessStatusCode();
        var progress = (await hiddenProgress.Content.ReadFromJsonAsync<LearningProgressResponse>())!;
        Assert.DoesNotContain(progress.Entries, entry => entry.ActivityId == setup.Activity.Id);

        StudentReportResponse? hiddenReport = null;
        using (var reportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (hiddenReport is null || hiddenReport.CompletedActivityCount != 0)
            {
                using var reportResponse = await setup.Student.GetAsync(
                    $"/api/reporting/classes/{setup.ClassId:D}/students/me", reportTimeout.Token);
                reportResponse.EnsureSuccessStatusCode();
                hiddenReport = await reportResponse.Content.ReadFromJsonAsync<StudentReportResponse>(
                    reportTimeout.Token);
                if (hiddenReport?.CompletedActivityCount == 0)
                {
                    break;
                }

                await Task.Delay(100, reportTimeout.Token);
            }
        }
        Assert.NotNull(hiddenReport);
        Assert.Equal(1, hiddenReport!.ActivityCount);
        Assert.Equal(0, hiddenReport.CompletedActivityCount);

        using (var releaseWait = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (DateTimeOffset.UtcNow < releaseAt)
            {
                await Task.Delay(50, releaseWait.Token);
            }
        }

        StudentReportResponse? releasedReport = null;
        using (var releaseReportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (releasedReport is null || releasedReport.CompletedActivityCount != 1)
            {
                using var reportResponse = await setup.Student.GetAsync(
                    $"/api/reporting/classes/{setup.ClassId:D}/students/me",
                    releaseReportTimeout.Token);
                reportResponse.EnsureSuccessStatusCode();
                releasedReport = await reportResponse.Content.ReadFromJsonAsync<StudentReportResponse>(
                    releaseReportTimeout.Token);
                if (releasedReport?.CompletedActivityCount == 1)
                {
                    break;
                }

                await Task.Delay(100, releaseReportTimeout.Token);
            }
        }
        Assert.NotNull(releasedReport);
        Assert.Equal(1, releasedReport!.PublishedGradeCount);
        Assert.Equal(1, releasedReport.CompletedActivityCount);

        using var teacherGradebookResponse = await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/gradebook");
        teacherGradebookResponse.EnsureSuccessStatusCode();
        var teacherGradebook = (await teacherGradebookResponse.Content
            .ReadFromJsonAsync<GradebookResponse>())!;
        var teacherCell = Assert.Single(Assert.Single(teacherGradebook.Students).Entries);
        Assert.Equal(grade.Score, teacherCell.Score);
    }

    [Fact]
    public async Task Essay_assignment_validates_group_topic_and_report_before_manual_grade()
    {
        var setup = await CreateActivitySetupAsync(includeSecondStudent: true);
        var policy = new ReportValidationPolicy
        {
            MinimumPages = 1,
            MinimumWords = 5,
            MinimumCharacters = 20,
            RequiredSections = ["Introduction", "Conclusion"],
            RequireTopicMatch = true
        };

        using var createAssignment = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/classes/{setup.ClassId:D}/essay-assignments", new
            {
                Title = "Realtime essay",
                Description = "A report for the live learning class",
                Deadline = (DateTimeOffset?)null,
                MaxScore = 100,
                MaxGroupSize = 2,
                AllowResubmission = true,
                ValidationPolicy = policy
            });
        Assert.Equal(HttpStatusCode.Created, createAssignment.StatusCode);
        var assignment = (await createAssignment.Content.ReadFromJsonAsync<EssayAssignmentResponse>())!;

        using var publishAssignment = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-assignments/{assignment.Id:D}/publish", new { });
        publishAssignment.EnsureSuccessStatusCode();

        using var createGroup = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups", new { Name = "Realtime team" });
        Assert.Equal(HttpStatusCode.Created, createGroup.StatusCode);
        var group = (await createGroup.Content.ReadFromJsonAsync<EssayGroupResponse>())!;
        Assert.Contains(group.Members, member => member.StudentId == setup.StudentId);

        using var addMember = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-groups/{group.Id:D}/members", new
            {
                StudentId = setup.SecondStudentId
            });
        Assert.Equal(HttpStatusCode.OK, addMember.StatusCode);
        group = (await addMember.Content.ReadFromJsonAsync<EssayGroupResponse>())!;
        Assert.Equal(2, group.Members.Length);

        using var registerTopic = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-groups/{group.Id:D}/topic", new
            {
                ProposedTitle = "  Realtime   Learning Systems  "
            });
        Assert.Equal(HttpStatusCode.OK, registerTopic.StatusCode);
        var topic = (await registerTopic.Content.ReadFromJsonAsync<EssayTopicResponse>())!;
        Assert.Equal(EssayTopicStatus.PendingReview, topic.Status);

        using var approveTopic = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-topics/{topic.Id:D}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approveTopic.StatusCode);
        topic = (await approveTopic.Content.ReadFromJsonAsync<EssayTopicResponse>())!;
        Assert.Equal("Realtime Learning Systems", topic.CanonicalTitle);

        using var lockTopic = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-topics/{topic.Id:D}/lock", new { });
        Assert.Equal(HttpStatusCode.OK, lockTopic.StatusCode);

        using var requestTopicChange = await PostWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-groups/{group.Id:D}/topic/change-request", new
            {
                ProposedTitle = "Realtime Learning Platform Systems"
            });
        Assert.Equal(HttpStatusCode.OK, requestTopicChange.StatusCode);
        topic = (await requestTopicChange.Content.ReadFromJsonAsync<EssayTopicResponse>())!;
        Assert.Equal(EssayTopicStatus.PendingReview, topic.Status);

        using var approveChangedTopic = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-topics/{topic.Id:D}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approveChangedTopic.StatusCode);
        topic = (await approveChangedTopic.Content.ReadFromJsonAsync<EssayTopicResponse>())!;
        Assert.Equal("Realtime Learning Platform Systems", topic.CanonicalTitle);
        using var lockChangedTopic = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-topics/{topic.Id:D}/lock", new { });
        Assert.Equal(HttpStatusCode.OK, lockChangedTopic.StatusCode);

        var invalidPdf = PdfReport("Wrong Topic", "Introduction", "Conclusion", "short");
        using var invalidUpload = await PostMultipartWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups/{group.Id:D}/submissions",
            invalidPdf, "wrong-topic.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpload.StatusCode);
        var invalidBody = await invalidUpload.Content.ReadAsStringAsync();
        Assert.Contains("does not match the approved canonical topic", invalidBody,
            StringComparison.Ordinal);
        var invalidPayload = System.Text.Json.JsonSerializer.Deserialize<EssayUploadErrorResponse>(invalidBody,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(invalidPayload?.Submission);
        Assert.Equal(EssaySubmissionStatus.Rejected, invalidPayload!.Submission!.Status);

        var validPdf = PdfReport("Realtime Learning Platform Systems", "Introduction", "Conclusion",
            "This report explains shared learning state and classroom progress.");
        using var validUpload = await PostMultipartWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups/{group.Id:D}/submissions",
            validPdf, "report.pdf");
        Assert.Equal(HttpStatusCode.Created, validUpload.StatusCode);
        var firstSubmission = (await validUpload.Content.ReadFromJsonAsync<EssaySubmissionResponse>())!;
        Assert.Equal(EssaySubmissionStatus.Submitted, firstSubmission.Status);
        Assert.Equal(2, firstSubmission.Version);
        Assert.NotEqual(Guid.Empty, firstSubmission.FileAssetId);

        using var reportFile = await setup.Teacher.GetAsync(
            $"/api/assessment/essay-submissions/{firstSubmission.Id:D}/file");
        Assert.Equal(HttpStatusCode.OK, reportFile.StatusCode);
        var reportFileResponse = await reportFile.Content.ReadFromJsonAsync<EssaySubmissionFileResponse>();
        Assert.Equal(firstSubmission.Id, reportFileResponse!.SubmissionId);
        Assert.StartsWith("https://files.test/", reportFileResponse.AccessUrl,
            StringComparison.Ordinal);

        using var resubmission = await PostMultipartWithCsrfAsync(setup.Student,
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups/{group.Id:D}/submissions",
            PdfReport("Realtime Learning Platform Systems", "Introduction", "Conclusion",
                "This revised report adds the requested evidence and discussion."), "report-v2.pdf");
        Assert.Equal(HttpStatusCode.Created, resubmission.StatusCode);
        var submission = (await resubmission.Content.ReadFromJsonAsync<EssaySubmissionResponse>())!;
        Assert.Equal(EssaySubmissionStatus.Submitted, submission.Status);
        Assert.Equal(3, submission.Version);

        using var submissions = await setup.Teacher.GetAsync(
            $"/api/assessment/essay-groups/{group.Id:D}/submissions");
        Assert.Equal(HttpStatusCode.OK, submissions.StatusCode);
        var history = (await submissions.Content.ReadFromJsonAsync<EssaySubmissionResponse[]>())!;
        Assert.Equal(3, history.Length);
        Assert.Contains(history, value => value.Status == EssaySubmissionStatus.Rejected && value.Version == 1);
        Assert.Contains(history, value => value.Status == EssaySubmissionStatus.Superseded && value.Version == 2);
        Assert.Contains(history, value => value.Status == EssaySubmissionStatus.Submitted && value.Version == 3);

        using var grade = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-submissions/{submission.Id:D}/grade", new
            {
                Score = 88m,
                Feedback = "Clear report with a well-defined topic."
            });
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        var gradeEntry = (await grade.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        Assert.Equal(88m, gradeEntry.Score);
        Assert.Equal(GradeSource.Manual, gradeEntry.Source);

        using var publishGrade = await PostWithCsrfAsync(setup.Teacher,
            $"/api/assessment/essay-grade-entries/{gradeEntry.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publishGrade.StatusCode);

        using var result = await setup.Student.GetAsync(
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups/{group.Id:D}/result");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var published = (await result.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        Assert.Equal(88m, published.Score);
        Assert.Equal(GradeEntryStatus.Published, published.Status);

        using var secondResult = await setup.SecondStudent!.GetAsync(
            $"/api/assessment/essay-assignments/{assignment.Id:D}/groups/{group.Id:D}/result");
        Assert.Equal(HttpStatusCode.OK, secondResult.StatusCode);
        var secondPublished = (await secondResult.Content.ReadFromJsonAsync<GradeEntryResponse>())!;
        Assert.Equal(88m, secondPublished.Score);
        Assert.Equal(GradeEntryStatus.Published, secondPublished.Status);

        StudentReportResponse? studentReport = null;
        using (var reportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (studentReport is null || studentReport.PublishedGradeCount != 1)
            {
                using var reportResponse = await setup.Student.GetAsync(
                    $"/api/reporting/classes/{setup.ClassId:D}/students/me", reportTimeout.Token);
                reportResponse.EnsureSuccessStatusCode();
                studentReport = await reportResponse.Content.ReadFromJsonAsync<StudentReportResponse>(
                    reportTimeout.Token);
                if (studentReport?.PublishedGradeCount == 1)
                {
                    break;
                }

                await Task.Delay(100, reportTimeout.Token);
            }
        }
        Assert.NotNull(studentReport);
        Assert.Equal(2, studentReport!.ActivityCount);
        Assert.Equal(1, studentReport.CompletedActivityCount);

        using var essayGradebookResponse = await setup.Student.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/gradebook/me");
        essayGradebookResponse.EnsureSuccessStatusCode();
        var essayGradebook = (await essayGradebookResponse.Content
            .ReadFromJsonAsync<GradebookResponse>())!;
        var essayItem = Assert.Single(essayGradebook.Items,
            item => item.EssayAssignmentId == assignment.Id);
        Assert.Equal(assignment.Id, essayItem.EssayAssignmentId);
        var essayCell = Assert.Single(Assert.Single(essayGradebook.Students).Entries,
            entry => entry.GradeItemId == essayItem.GradeItemId);
        Assert.Equal(88m, essayCell.Score);
    }

    private static byte[] PdfReport(string title, string firstSection, string secondSection, string body)
    {
        var content = $"%PDF-1.4\n1 0 obj << /Type /Page >> endobj\nBT ({title}) Tj ET\n" +
                      $"BT ({firstSection}) Tj ET\nBT ({secondSection}) Tj ET\nBT ({body}) Tj ET\n%%EOF";
        return Encoding.ASCII.GetBytes(content);
    }

    private async Task<HttpResponseMessage> PostMultipartWithCsrfAsync(HttpClient client, string path,
        byte[] bytes, string fileName)
    {
        var authCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(cookie => cookie.StartsWith("__Host-rlms=", StringComparison.Ordinal));
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);
        return await client.PostAsync(path, content);
    }

    private sealed record EssayUploadErrorResponse(string Error, EssaySubmissionResponse? Submission);

    private async Task<ActivitySetup> CreateActivitySetupAsync(
        QuestionType questionType = QuestionType.SingleChoice, bool includeSecondStudent = false,
        AssessmentActivityKind activityKind = AssessmentActivityKind.Quiz,
        ExamPolicy? examPolicy = null)
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher, await fixture.LoginCookieAsync());
        var course = await CreatePublishedCourseAsync(teacher);
        var learningClass = await CreateClassAsync(teacher, course.Id);

        using var assignment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/teachers", new
            {
                TeacherId = organization.AdministratorId
            });
        assignment.EnsureSuccessStatusCode();

        var studentEmail = $"activity-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "activity student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enrollment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        enrollment.EnsureSuccessStatusCode();

        Guid secondStudentId = Guid.Empty;
        HttpClient? secondStudentClient = null;
        if (includeSecondStudent)
        {
            var secondEmail = $"activity-second-{Guid.NewGuid():N}@rlms.test";
            var second = await fixture.CreateTestAccountAsync(secondEmail,
                "second activity student password", "STUDENT");
            secondStudentId = second.UserId;
            using var secondEnrollment = await PostWithCsrfAsync(teacher,
                $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
                {
                    StudentId = second.UserId
                });
            secondEnrollment.EnsureSuccessStatusCode();
            secondStudentClient = fixture.Client();
            FoundationFixture.SetCookies(secondStudentClient,
                await fixture.LoginCookieAsync(secondEmail, "second activity student password"));
        }

        var questionOptions = questionType is QuestionType.SingleChoice or QuestionType.MultipleChoice
            ? new object[]
            {
                new { Key = "A", Text = "3" },
                new { Key = "B", Text = "4" }
            }
            : [];
        var correctAnswers = questionType is QuestionType.SingleChoice or QuestionType.MultipleChoice
            ? new[] { "B" }
            : [];
        using var createQuestion = await PostWithCsrfAsync(teacher, "/api/assessment/question-bank", new
        {
            CourseId = course.Id,
            Topic = "Arithmetic",
            Difficulty = "Easy",
            Prompt = "What is two plus two?",
            Type = (int)questionType,
            Options = questionOptions,
            CorrectAnswers = correctAnswers,
        });
        createQuestion.EnsureSuccessStatusCode();
        var question = (await createQuestion.Content.ReadFromJsonAsync<QuestionBankItemDetailResponse>())!;
        var version = Assert.Single(question.Versions);

        using var createActivity = await PostWithCsrfAsync(teacher,
            $"/api/assessment/classes/{learningClass.Id:D}/activities", new
            {
                Title = "Arithmetic check",
                Description = "A short quiz",
                Kind = (int)activityKind,
                OpensAt = (DateTimeOffset?)null,
                DueAt = (DateTimeOffset?)null,
                TimeLimit = (TimeSpan?)null,
                ExamPolicy = examPolicy
            });
        createActivity.EnsureSuccessStatusCode();
        var activity = (await createActivity.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        using var addQuestion = await PostWithCsrfAsync(teacher,
            $"/api/assessment/activities/{activity.Id:D}/questions", new
            {
                QuestionVersionId = version.Id,
                Order = 0,
                Points = 5m
            });
        addQuestion.EnsureSuccessStatusCode();
        activity = (await addQuestion.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        using var publish = await PostWithCsrfAsync(teacher,
            $"/api/assessment/activities/{activity.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        activity = (await publish.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        return new ActivitySetup(teacher, studentClient, learningClass.Id, organization.AdministratorId,
            student.UserId, activity, question.Item.Id, version, secondStudentClient, secondStudentId);
    }

    private async Task<CourseResponse> CreatePublishedCourseAsync(HttpClient client)
    {
        using var create = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = $"ACT-{Guid.NewGuid():N}"[..14],
            Title = "Activity Course",
            Description = ""
        });
        create.EnsureSuccessStatusCode();
        var course = (await create.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var publish = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        return (await publish.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private async Task<ClassResponse> CreateClassAsync(HttpClient client, Guid courseId)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var create = await PostWithCsrfAsync(client, "/api/academics/classes", new
        {
            CourseId = courseId,
            Code = $"ACT-{Guid.NewGuid():N}"[..14],
            Name = "Activity Class",
            StartDate = startDate,
            EndDate = startDate.AddMonths(1)
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<ClassResponse>())!;
    }

    private async Task<HttpClient> CreateAssignedTeacherAsync(ActivitySetup setup)
    {
        var email = $"activity-teacher-{Guid.NewGuid():N}@rlms.test";
        var password = "activity teacher password";
        var teacher = await fixture.CreateTestAccountAsync(email, password, "TEACHER");
        using var assignment = await PostWithCsrfAsync(setup.Teacher,
            $"/api/academics/classes/{setup.ClassId:D}/teachers", new
            {
                TeacherId = teacher.UserId
            });
        assignment.EnsureSuccessStatusCode();

        var client = fixture.Client();
        FoundationFixture.SetCookies(client,
            await fixture.LoginCookieAsync(email, password));
        return client;
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body)
    {
        var authCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(cookie => cookie.StartsWith("__Host-rlms=", StringComparison.Ordinal));
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.PostAsJsonAsync(path, body);
    }

    private async Task<HttpResponseMessage> PutWithCsrfAsync(HttpClient client, string path, object body)
    {
        var authCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(cookie => cookie.StartsWith("__Host-rlms=", StringComparison.Ordinal));
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.PutAsJsonAsync(path, body);
    }

    private sealed record ActivitySetup(HttpClient Teacher, HttpClient Student, Guid ClassId,
        Guid TeacherId, Guid StudentId, AssessmentActivityResponse Activity, Guid QuestionItemId,
        QuestionVersionResponse QuestionVersion, HttpClient? SecondStudent, Guid SecondStudentId);
}
