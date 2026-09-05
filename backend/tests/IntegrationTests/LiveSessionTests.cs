using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LearningPlatform.IntegrationTests;

public sealed class LiveSessionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Started_session_allows_assigned_teacher_and_enrolled_student_with_ephemeral_presence()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var teacher = await AuthenticatedClientAsync();
        var course = await CreatePublishedCourseAsync(teacher);
        var learningClass = await CreateClassAsync(teacher, course.Id);

        using var assignment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/teachers", new
            {
                TeacherId = organization.AdministratorId
            });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);

        var studentEmail = $"live-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "live student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enrollment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);

        using var create = await PostWithCsrfAsync(teacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new
            {
                TeacherId = organization.AdministratorId,
                ClassScheduleId = (Guid?)null
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var scheduled = await create.Content.ReadFromJsonAsync<LiveSessionResponse>();
        Assert.NotNull(scheduled);
        Assert.Equal(LiveSessionStatus.Scheduled, scheduled.Status);

        using var beforeStart = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{scheduled.Id:D}/join", new { });
        Assert.Equal(HttpStatusCode.Conflict, beforeStart.StatusCode);

        using var start = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{scheduled.Id:D}/start", new { });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<LiveSessionResponse>();
        Assert.Equal(LiveSessionStatus.Started, started!.Status);

        using var teacherJoin = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{scheduled.Id:D}/join", new { });
        Assert.Equal(HttpStatusCode.OK, teacherJoin.StatusCode);
        var teacherJoinResponse = await teacherJoin.Content.ReadFromJsonAsync<LiveSessionJoinResponse>();
        Assert.NotNull(teacherJoinResponse);
        Assert.Equal(started.Id, teacherJoinResponse.Session.Id);
        Assert.Equal(started.MediaRoomId, teacherJoinResponse.Media.RoomId);
        Assert.Equal("ws://localhost:57880", teacherJoinResponse.Media.ServerUrl);
        Assert.NotEmpty(teacherJoinResponse.Media.AccessToken);
        Assert.True(teacherJoinResponse.PresenceRegistered);

        var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var teacherPresenceKey = LivePresenceStore.Key(organization.OrganizationId, scheduled.Id,
            organization.AdministratorId);
        Assert.NotNull(await redis.KeyTimeToLiveAsync(teacherPresenceKey));

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        using var studentJoin = await PostWithCsrfAsync(studentClient,
            $"/api/classroom/sessions/{scheduled.Id:D}/join", new { });
        Assert.Equal(HttpStatusCode.OK, studentJoin.StatusCode);
        var studentJoinResponse = await studentJoin.Content.ReadFromJsonAsync<LiveSessionJoinResponse>();
        Assert.True(studentJoinResponse!.PresenceRegistered);

        await using (var connection = new HubConnectionBuilder()
                         .WithUrl($"https://localhost/hubs/live/{scheduled.Id:D}", options =>
                         {
                             options.Transports = HttpTransportType.LongPolling;
                             options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                             options.Headers["Cookie"] = studentClient.DefaultRequestHeaders
                                 .GetValues("Cookie").Single();
                         }).Build())
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.StartAsync(timeout.Token);
            Assert.Equal(HubConnectionState.Connected, connection.State);
            await connection.InvokeAsync("RefreshPresence", timeout.Token);
            await connection.StopAsync(timeout.Token);
        }

        using var leave = await PostWithCsrfAsync(studentClient,
            $"/api/classroom/sessions/{scheduled.Id:D}/leave", new { });
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Null(await redis.KeyTimeToLiveAsync(LivePresenceStore.Key(organization.OrganizationId,
            scheduled.Id, student.UserId)));

        using var end = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{scheduled.Id:D}/end", new { });
        Assert.Equal(HttpStatusCode.OK, end.StatusCode);
        var ended = await end.Content.ReadFromJsonAsync<LiveSessionResponse>();
        Assert.Equal(LiveSessionStatus.Ended, ended!.Status);

        using var afterEnd = await PostWithCsrfAsync(studentClient,
            $"/api/classroom/sessions/{scheduled.Id:D}/join", new { });
        Assert.Equal(HttpStatusCode.Conflict, afterEnd.StatusCode);
    }

    [Fact]
    public async Task Session_access_requires_class_membership_and_management_is_teacher_scoped()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var administrator = await AuthenticatedClientAsync();
        var course = await CreatePublishedCourseAsync(administrator);
        var learningClass = await CreateClassAsync(administrator, course.Id);
        var teacherEmail = $"live-teacher-{Guid.NewGuid():N}@rlms.test";
        var teacherPassword = "live teacher password";
        var teacherAccount = await fixture.CreateTestAccountAsync(teacherEmail, teacherPassword, "TEACHER");
        using var assignment = await PostWithCsrfAsync(administrator,
            $"/api/academics/classes/{learningClass.Id:D}/teachers", new
            {
                TeacherId = teacherAccount.UserId
            });
        assignment.EnsureSuccessStatusCode();
        using var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher,
            await fixture.LoginCookieAsync(teacherEmail, teacherPassword));

        var unassignedTeacherEmail = $"live-unassigned-{Guid.NewGuid():N}@rlms.test";
        var unassignedTeacherPassword = "live unassigned password";
        await fixture.CreateTestAccountAsync(unassignedTeacherEmail, unassignedTeacherPassword, "TEACHER");
        using var unassignedTeacher = fixture.Client();
        FoundationFixture.SetCookies(unassignedTeacher,
            await fixture.LoginCookieAsync(unassignedTeacherEmail, unassignedTeacherPassword));
        using var deniedCreate = await PostWithCsrfAsync(unassignedTeacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new { });
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);

        using var create = await PostWithCsrfAsync(teacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new
            {
                TeacherId = teacherAccount.UserId
            });
        create.EnsureSuccessStatusCode();
        var session = (await create.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
        using var start = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{session.Id:D}/start", new { });
        start.EnsureSuccessStatusCode();

        var outsiderEmail = $"live-outsider-{Guid.NewGuid():N}@rlms.test";
        var outsiderPassword = "live outsider password";
        await fixture.CreateTestAccountAsync(outsiderEmail, outsiderPassword, "STUDENT");
        using var outsider = fixture.Client();
        FoundationFixture.SetCookies(outsider,
            await fixture.LoginCookieAsync(outsiderEmail, outsiderPassword));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.GetAsync($"/api/classroom/sessions/{session.Id:D}")).StatusCode);
        using var deniedJoin = await PostWithCsrfAsync(outsider,
            $"/api/classroom/sessions/{session.Id:D}/join", new { });
        Assert.Equal(HttpStatusCode.Forbidden, deniedJoin.StatusCode);
    }

    private async Task<CourseResponse> CreatePublishedCourseAsync(HttpClient client)
    {
        using var create = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = $"LIVE-{Guid.NewGuid():N}"[..14],
            Title = "Live Classroom Course",
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
            Code = $"LIVE-{Guid.NewGuid():N}"[..14],
            Name = "Live Classroom",
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
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.PostAsJsonAsync(path, body);
    }
}
