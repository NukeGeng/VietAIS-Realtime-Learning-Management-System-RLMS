using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Assessment.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace LearningPlatform.IntegrationTests;

public sealed class AssessmentActivityTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Teacher_can_publish_immutable_activity_and_student_can_save_and_submit_attempt()
    {
        var setup = await CreateActivitySetupAsync();

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
                             options.Headers["Cookie"] = setup.Teacher.DefaultRequestHeaders
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
        Assert.DoesNotContain("Answers", await (await setup.Teacher.GetAsync(
            $"/api/assessment/classes/{setup.ClassId:D}/activities/{setup.Activity.Id:D}/overview"))
            .Content.ReadAsStringAsync());

        using var detailResponse = await setup.Teacher.GetAsync(
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
                options.Headers["Cookie"] = setup.Teacher.DefaultRequestHeaders
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

    private async Task<ActivitySetup> CreateActivitySetupAsync(bool includeSecondStudent = false)
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

        using var createQuestion = await PostWithCsrfAsync(teacher, "/api/assessment/question-bank", new
        {
            CourseId = course.Id,
            Topic = "Arithmetic",
            Difficulty = "Easy",
            Prompt = "What is two plus two?",
            Type = (int)QuestionType.SingleChoice,
            Options = new[]
            {
                new { Key = "A", Text = "3" },
                new { Key = "B", Text = "4" }
            },
            CorrectAnswers = new[] { "B" }
        });
        createQuestion.EnsureSuccessStatusCode();
        var question = (await createQuestion.Content.ReadFromJsonAsync<QuestionBankItemDetailResponse>())!;
        var version = Assert.Single(question.Versions);

        using var createActivity = await PostWithCsrfAsync(teacher,
            $"/api/assessment/classes/{learningClass.Id:D}/activities", new
            {
                Title = "Arithmetic check",
                Description = "A short quiz",
                Kind = (int)AssessmentActivityKind.Quiz,
                OpensAt = (DateTimeOffset?)null,
                DueAt = (DateTimeOffset?)null,
                TimeLimit = (TimeSpan?)null
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
