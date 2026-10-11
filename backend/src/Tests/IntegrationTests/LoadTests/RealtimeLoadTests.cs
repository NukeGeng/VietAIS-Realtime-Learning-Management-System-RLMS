using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace LearningPlatform.IntegrationTests.LoadTests;

public sealed class RealtimeLoadTests(FoundationFixture fixture, ITestOutputHelper output) : IClassFixture<FoundationFixture>
{
    [Fact]
    [Trait("Category", "Load")]
    public async Task Mass_join_and_answer_burst_keep_realtime_state_consistent()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var teacher = await AuthenticatedClientAsync();
        var course = await CreatePublishedCourseAsync(teacher);
        var learningClass = await CreateClassAsync(teacher, course.Id);
        await AssignTeacherAsync(teacher, learningClass.Id, organization.AdministratorId);

        using var createSession = await PostWithCsrfAsync(teacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new { });
        createSession.EnsureSuccessStatusCode();
        var liveSession = (await createSession.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
        using var startSession = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{liveSession.Id:D}/start", new { });
        startSession.EnsureSuccessStatusCode();

        var students = new List<StudentClient>();
        try
        {
            var studentCount = ReadLoadStudentCount();
            for (var index = 0; index < studentCount; index++)
            {
                var email = $"load-student-{Guid.NewGuid():N}@rlms.test";
                var account = await fixture.CreateTestAccountAsync(email,
                    "load student password", "STUDENT");
                using var enrollment = await PostWithCsrfAsync(teacher,
                    $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
                    {
                        StudentId = account.UserId
                    });
                enrollment.EnsureSuccessStatusCode();
                var client = fixture.Client();
                FoundationFixture.SetCookies(client,
                    await fixture.LoginCookieAsync(email, "load student password"));
                students.Add(new StudentClient(client, account.UserId));
            }

            var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            await redis.KeyDeleteAsync(LiveSessionSnapshotCache.Key(organization.OrganizationId,
                liveSession.Id));

            var joinTimer = Stopwatch.StartNew();
            var joins = await Task.WhenAll(students.Select(async student =>
            {
                using var response = await PostWithCsrfAsync(student.Client,
                    $"/api/classroom/sessions/{liveSession.Id:D}/join", new { });
                return response.StatusCode;
            }));
            joinTimer.Stop();
            Assert.All(joins, status => Assert.Equal(HttpStatusCode.OK, status));
            Assert.True(await redis.KeyExistsAsync(LiveSessionSnapshotCache.Key(
                organization.OrganizationId, liveSession.Id)));

            var question = await CreateQuestionAsync(teacher, course.Id);
            var activity = await CreatePublishedActivityAsync(teacher, learningClass.Id, question);

            var startTimer = Stopwatch.StartNew();
            var starts = await Task.WhenAll(students.Select(async student =>
            {
                using var response = await PostWithCsrfAsync(student.Client,
                    $"/api/assessment/activities/{activity.Id:D}/attempt/start", new { });
                return response.StatusCode;
            }));
            startTimer.Stop();
            Assert.All(starts, status => Assert.Equal(HttpStatusCode.OK, status));

            var answerTimer = Stopwatch.StartNew();
            var activityQuestionId = activity.Questions.Single().Id;
            var answers = await Task.WhenAll(students.Select(async student =>
            {
                using var response = await PutWithCsrfAsync(student.Client,
                    $"/api/assessment/activities/{activity.Id:D}/attempt/answers/{activityQuestionId:D}",
                    new SaveAnswerRequest("B"));
                return response.StatusCode;
            }));
            answerTimer.Stop();
            Assert.All(answers, status => Assert.Equal(HttpStatusCode.OK, status));

            var overviewReads = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
            {
                using var response = await teacher.GetAsync(
                    $"/api/assessment/classes/{learningClass.Id:D}/activities/{activity.Id:D}/overview");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ClassOverviewResponse>();
            }));
            Assert.All(overviewReads, overview => Assert.NotNull(overview));

            ClassOverviewResponse? overview = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (overview is null || (overview.Students ?? []).Any(student =>
                       student.Status != StudentActivityStatus.InProgress || student.AnsweredCount != 1))
            {
                using var response = await teacher.GetAsync(
                    $"/api/assessment/classes/{learningClass.Id:D}/activities/{activity.Id:D}/overview",
                    timeout.Token);
                response.EnsureSuccessStatusCode();
                overview = await response.Content.ReadFromJsonAsync<ClassOverviewResponse>(timeout.Token);
                if (overview is not null && overview.Students.Length == students.Count &&
                    overview.Students.All(student => student.Status == StudentActivityStatus.InProgress &&
                        student.AnsweredCount == 1))
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }

            var finalOverview = overview ?? throw new InvalidOperationException("Overview did not load.");
            Assert.Equal(students.Count, finalOverview.Students.Length);
            Assert.All(finalOverview.Students, student =>
            {
                Assert.Equal(StudentActivityStatus.InProgress, student.Status);
                Assert.Equal(1, student.AnsweredCount);
            });
            Assert.Equal(students.Count, finalOverview.QuestionProgress.Single().AnsweredStudentCount);
            output.WriteLine($"Realtime load: students={students.Count}, joins={joinTimer.ElapsedMilliseconds}ms, " +
                $"starts={startTimer.ElapsedMilliseconds}ms, answers={answerTimer.ElapsedMilliseconds}ms");
        }
        finally
        {
            foreach (var student in students)
            {
                student.Client.Dispose();
            }
        }
    }

    [Fact]
    [Trait("Category", "Load")]
    public async Task Live_session_reconnect_restores_presence_after_a_disconnect()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var teacher = await AuthenticatedClientAsync();
        var course = await CreatePublishedCourseAsync(teacher);
        var learningClass = await CreateClassAsync(teacher, course.Id);
        await AssignTeacherAsync(teacher, learningClass.Id, organization.AdministratorId);

        var email = $"reconnect-student-{Guid.NewGuid():N}@rlms.test";
        var student = await fixture.CreateTestAccountAsync(email, "reconnect password", "STUDENT");
        using var enrollment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        enrollment.EnsureSuccessStatusCode();
        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(email, "reconnect password"));

        using var createSession = await PostWithCsrfAsync(teacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new { });
        createSession.EnsureSuccessStatusCode();
        var liveSession = (await createSession.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
        using var startSession = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{liveSession.Id:D}/start", new { });
        startSession.EnsureSuccessStatusCode();

        var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var presenceKey = LivePresenceStore.Key(organization.OrganizationId, liveSession.Id,
            student.UserId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using (var first = BuildLiveConnection(studentClient, liveSession.Id))
        {
            await first.StartAsync(timeout.Token);
            Assert.Equal(HubConnectionState.Connected, first.State);
            await WaitForPresenceAsync(redis, presenceKey, timeout.Token);
            await first.StopAsync(timeout.Token);
        }

        await WaitForMissingPresenceAsync(redis, presenceKey, timeout.Token);

        await using (var reconnected = BuildLiveConnection(studentClient, liveSession.Id))
        {
            await reconnected.StartAsync(timeout.Token);
            Assert.Equal(HubConnectionState.Connected, reconnected.State);
            var ttl = await WaitForPresenceAsync(redis, presenceKey, timeout.Token);
            Assert.True(ttl > TimeSpan.Zero);
            await reconnected.StopAsync(timeout.Token);
        }
    }

    private async Task<QuestionVersionResponse> CreateQuestionAsync(HttpClient teacher, Guid courseId)
    {
        using var create = await PostWithCsrfAsync(teacher, "/api/assessment/question-bank", new
        {
            CourseId = courseId,
            Topic = "Load testing",
            Difficulty = "Easy",
            Prompt = "Which option is correct?",
            Type = (int)QuestionType.SingleChoice,
            Options = new[]
            {
                new { Key = "A", Text = "Incorrect" },
                new { Key = "B", Text = "Correct" }
            },
            CorrectAnswers = new[] { "B" }
        });
        create.EnsureSuccessStatusCode();
        var item = (await create.Content.ReadFromJsonAsync<QuestionBankItemDetailResponse>())!;
        return Assert.Single(item.Versions);
    }

    private async Task<AssessmentActivityResponse> CreatePublishedActivityAsync(HttpClient teacher,
        Guid classId, QuestionVersionResponse question)
    {
        using var create = await PostWithCsrfAsync(teacher,
            $"/api/assessment/classes/{classId:D}/activities", new
            {
                Title = "Realtime load activity",
                Description = "A workload test activity",
                Kind = (int)AssessmentActivityKind.Quiz,
                OpensAt = (DateTimeOffset?)null,
                DueAt = (DateTimeOffset?)null,
                TimeLimit = (TimeSpan?)null,
                ExamPolicy = (ExamPolicy?)null
            });
        create.EnsureSuccessStatusCode();
        var activity = (await create.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        using var addQuestion = await PostWithCsrfAsync(teacher,
            $"/api/assessment/activities/{activity.Id:D}/questions", new
            {
                QuestionVersionId = question.Id,
                Order = 0,
                Points = 1m
            });
        addQuestion.EnsureSuccessStatusCode();
        activity = (await addQuestion.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;

        using var publish = await PostWithCsrfAsync(teacher,
            $"/api/assessment/activities/{activity.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        return (await publish.Content.ReadFromJsonAsync<AssessmentActivityResponse>())!;
    }

    private async Task AssignTeacherAsync(HttpClient teacher, Guid classId, Guid teacherId)
    {
        using var assignment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{classId:D}/teachers", new { TeacherId = teacherId });
        assignment.EnsureSuccessStatusCode();
    }

    private async Task<CourseResponse> CreatePublishedCourseAsync(HttpClient teacher)
    {
        using var create = await PostWithCsrfAsync(teacher, "/api/academics/courses", new
        {
            Code = $"LOAD-{Guid.NewGuid():N}"[..14],
            Title = "Realtime load course",
            Description = ""
        });
        create.EnsureSuccessStatusCode();
        var course = (await create.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var publish = await PostWithCsrfAsync(teacher,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        return (await publish.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private async Task<ClassResponse> CreateClassAsync(HttpClient teacher, Guid courseId)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var create = await PostWithCsrfAsync(teacher, "/api/academics/classes", new
        {
            CourseId = courseId,
            Code = $"LOAD-{Guid.NewGuid():N}"[..14],
            Name = "Realtime load class",
            StartDate = startDate,
            EndDate = startDate.AddMonths(1)
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<ClassResponse>())!;
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Client();
        FoundationFixture.SetCookies(client, await fixture.LoginCookieAsync());
        return client;
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body)
    {
        var authCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(cookie => cookie.StartsWith("__Host-rlms=", StringComparison.Ordinal));
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
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.PutAsJsonAsync(path, body);
    }

    private HubConnection BuildLiveConnection(HttpClient client, Guid sessionId) =>
        new HubConnectionBuilder()
            .WithUrl($"https://localhost/hubs/live/{sessionId:D}", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                options.Headers["Cookie"] = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            })
            .Build();

    private static async Task<TimeSpan> WaitForPresenceAsync(IDatabase redis, string key,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var ttl = await redis.KeyTimeToLiveAsync(key);
            if (ttl is { } value && value > TimeSpan.Zero)
            {
                return value;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private static async Task WaitForMissingPresenceAsync(IDatabase redis, string key,
        CancellationToken cancellationToken)
    {
        while (await redis.KeyExistsAsync(key))
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private static int ReadLoadStudentCount()
    {
        return int.TryParse(Environment.GetEnvironmentVariable("RLMS_LOAD_STUDENTS"), out var count)
            ? Math.Clamp(count, 4, 64)
            : 16;
    }

    private sealed record StudentClient(HttpClient Client, Guid UserId);
}
