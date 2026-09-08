using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Communication.Api;
using LearningPlatform.Communication.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using LearningPlatform.Reporting.Domain;
using Wolverine;

namespace LearningPlatform.IntegrationTests;

public sealed class ClassroomInteractionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Chat_and_raise_hand_are_member_scoped_and_realtime_state_is_durable()
    {
        var setup = await CreateStartedSessionAsync();

        using var send = await PostWithCsrfAsync(setup.Student, setup.Session.Id,
            "/messages", new SendChatMessageRequest("  hello class  "));
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        var message = await send.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.NotNull(message);
        Assert.Equal("hello class", message.Body);
        Assert.Equal(setup.StudentId, message.SenderId);

        using var messages = await setup.Student.GetAsync(
            $"/api/classroom/sessions/{setup.Session.Id:D}/messages");
        Assert.Equal(HttpStatusCode.OK, messages.StatusCode);
        var messageList = await messages.Content.ReadFromJsonAsync<ChatMessageResponse[]>();
        Assert.Single(messageList!);
        Assert.Equal("hello class", messageList![0].Body);

        using var invalid = await PostWithCsrfAsync(setup.Student, setup.Session.Id,
            "/messages", new SendChatMessageRequest("   "));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var oversized = await PostWithCsrfAsync(setup.Student, setup.Session.Id,
            "/messages", new SendChatMessageRequest(new string('x', ChatRules.MaxMessageLength + 1)));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var raise = await PostWithCsrfAsync(setup.Student, setup.Session.Id,
            "/raise-hand", new { });
        Assert.Equal(HttpStatusCode.OK, raise.StatusCode);
        var raised = await raise.Content.ReadFromJsonAsync<RaiseHandResponse>();
        Assert.Equal(RaiseHandStatus.Raised, raised!.Status);

        using var list = await setup.Teacher.GetAsync(
            $"/api/classroom/sessions/{setup.Session.Id:D}/raise-hands");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var hands = await list.Content.ReadFromJsonAsync<RaiseHandResponse[]>();
        Assert.Single(hands!);
        Assert.Equal(setup.StudentId, hands![0].StudentId);

        using var clear = await PostWithCsrfAsync(setup.Teacher, setup.Session.Id,
            $"/raise-hands/{setup.StudentId:D}/clear", new { });
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        using var afterClear = await setup.Teacher.GetAsync(
            $"/api/classroom/sessions/{setup.Session.Id:D}/raise-hands");
        var remainingHands = await afterClear.Content.ReadFromJsonAsync<RaiseHandResponse[]>();
        Assert.Empty(remainingHands!);

        var outsiderEmail = $"interaction-outsider-{Guid.NewGuid():N}@rlms.test";
        var outsiderPassword = "interaction outsider password";
        await fixture.CreateTestAccountAsync(outsiderEmail, outsiderPassword, "STUDENT");
        using var outsider = fixture.Client();
        FoundationFixture.SetCookies(outsider,
            await fixture.LoginCookieAsync(outsiderEmail, outsiderPassword));
        using var denied = await PostWithCsrfAsync(outsider, setup.Session.Id,
            "/messages", new SendChatMessageRequest("should be denied"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Attendance_is_durable_async_and_can_be_adjusted_by_assigned_teacher()
    {
        var setup = await CreateStartedSessionAsync(includeSecondStudent: true);

        using var studentDenied = await setup.Student.GetAsync(
            $"/api/classroom/sessions/{setup.Session.Id:D}/attendance");
        Assert.Equal(HttpStatusCode.Forbidden, studentDenied.StatusCode);

        using var join = await PostWithCsrfAsync(setup.Student, setup.Session.Id, "/join", new { });
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        await Task.Delay(100);
        using var leave = await PostWithCsrfAsync(setup.Student, setup.Session.Id, "/leave", new { });
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        using var end = await PostWithCsrfAsync(setup.Teacher, setup.Session.Id, "/end", new { });
        Assert.Equal(HttpStatusCode.OK, end.StatusCode);

        AttendanceSummaryResponse? summary = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (summary is null || summary.Records.Count < 2)
        {
            using var response = await setup.Teacher.GetAsync(
                $"/api/classroom/sessions/{setup.Session.Id:D}/attendance", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            summary = await response.Content.ReadFromJsonAsync<AttendanceSummaryResponse>(timeout.Token);
            if (summary!.Records.Count >= 2)
            {
                break;
            }

            await Task.Delay(100, timeout.Token);
        }

        var studentRecord = Assert.Single(summary!.Records,
            record => record.StudentId == setup.StudentId);
        Assert.NotNull(studentRecord.FirstJoinedAt);
        Assert.NotNull(studentRecord.LastLeftAt);
        Assert.True(studentRecord.TotalDuration >= TimeSpan.Zero);
        Assert.True(new[] { AttendanceStatus.Present, AttendanceStatus.Late }.Contains(studentRecord.Status));

        var absentRecord = Assert.Single(summary.Records,
            record => record.StudentId == setup.SecondStudentId);
        Assert.Equal(AttendanceStatus.Absent, absentRecord.Status);
        Assert.Equal(2, summary!.TotalStudents);
        Assert.True(summary.AttendancePercentage >= 0);

        using var adjust = await PostWithCsrfAsync(setup.Teacher, setup.Session.Id,
            $"/attendance/{setup.SecondStudentId:D}", new AttendanceAdjustmentRequest("Excused", "Approved absence"));
        Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);
        var adjusted = await adjust.Content.ReadFromJsonAsync<AttendanceRecordResponse>();
        Assert.Equal(AttendanceStatus.Excused, adjusted!.Status);
        Assert.Equal(setup.TeacherId, adjusted.AdjustedBy);
        Assert.Equal("Approved absence", adjusted.AdjustmentReason);

        using var afterAdjustment = await setup.Teacher.GetAsync(
            $"/api/classroom/sessions/{setup.Session.Id:D}/attendance");
        var adjustedSummary = await afterAdjustment.Content.ReadFromJsonAsync<AttendanceSummaryResponse>();
        Assert.Equal(AttendanceStatus.Excused,
            Assert.Single(adjustedSummary!.Records, record => record.StudentId == setup.SecondStudentId).Status);
    }

    [Fact]
    public async Task Attendance_completion_publishes_reporting_refresh_after_records_are_finalized()
    {
        var setup = await CreateStartedSessionAsync(includeSecondStudent: true);
        var organization = await fixture.EnsureBootstrappedAsync();
        var store = fixture.Services.GetRequiredService<IDocumentStore>();

        using var join = await PostWithCsrfAsync(setup.Student, setup.Session.Id, "/join", new { });
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        using (var attendanceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (true)
            {
                await using var query = store.QuerySession(organization.OrganizationId.ToString("D"));
                if (await query.LoadAsync<AttendanceRecord>(
                        ClassroomConstants.AttendanceRecordId(setup.Session.Id, setup.StudentId),
                        attendanceTimeout.Token) is not null)
                {
                    break;
                }

                await Task.Delay(100, attendanceTimeout.Token);
            }
        }

        var endedAt = DateTimeOffset.UtcNow;
        await using (var session = store.LightweightSession(organization.OrganizationId.ToString("D")))
        {
            var liveSession = await session.LoadAsync<LiveSession>(setup.Session.Id);
            Assert.NotNull(liveSession);
            liveSession!.Status = LiveSessionStatus.Ended;
            liveSession.EndedAt = endedAt;
            liveSession.UpdatedAt = endedAt;
            session.Update(liveSession);
            await session.SaveChangesAsync();
        }

        await using var busScope = fixture.Services.CreateAsyncScope();
        await AttendanceSessionEndedHandler.Handle(
            new LiveSessionEnded(organization.OrganizationId, setup.Session.Id, endedAt),
            store,
            busScope.ServiceProvider.GetRequiredService<IMessageBus>(),
            CancellationToken.None);

        ClassReportSnapshot? report = null;
        using (var reportTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (report is null || report.AttendanceSessionCount != 1 ||
                   report.AttendancePresentCount != 1 || report.AttendanceAbsentCount != 1)
            {
                await using var query = store.QuerySession(organization.OrganizationId.ToString("D"));
                report = await query.LoadAsync<ClassReportSnapshot>(
                    ReportingConstants.ClassSnapshotId(setup.ClassId), reportTimeout.Token);
                if (report is not null && report.AttendanceSessionCount == 1 &&
                    report.AttendancePresentCount == 1 && report.AttendanceAbsentCount == 1)
                {
                    break;
                }

                await Task.Delay(100, reportTimeout.Token);
            }
        }

        Assert.NotNull(report);
        Assert.Equal(1, report!.AttendanceSessionCount);
        Assert.Equal(1, report.AttendancePresentCount);
        Assert.Equal(1, report.AttendanceAbsentCount);
    }

    private async Task<SessionSetup> CreateStartedSessionAsync(bool includeSecondStudent = false)
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

        var studentEmail = $"interaction-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "interaction student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enrollment = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        enrollment.EnsureSuccessStatusCode();

        Guid secondStudentId = Guid.Empty;
        if (includeSecondStudent)
        {
            var secondEmail = $"interaction-absent-{Guid.NewGuid():N}@rlms.test";
            var second = await fixture.CreateTestAccountAsync(secondEmail, "interaction absent password", "STUDENT");
            secondStudentId = second.UserId;
            using var secondEnrollment = await PostWithCsrfAsync(teacher,
                $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
                {
                    StudentId = second.UserId
                });
            secondEnrollment.EnsureSuccessStatusCode();
        }

        using var create = await PostWithCsrfAsync(teacher,
            $"/api/classroom/classes/{learningClass.Id:D}/sessions", new
            {
                TeacherId = organization.AdministratorId,
                ClassScheduleId = (Guid?)null
            });
        create.EnsureSuccessStatusCode();
        var session = (await create.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
        using var start = await PostWithCsrfAsync(teacher,
            $"/api/classroom/sessions/{session.Id:D}/start", new { });
        start.EnsureSuccessStatusCode();

        var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        return new SessionSetup(teacher, studentClient, session, learningClass.Id,
            organization.AdministratorId, student.UserId, secondStudentId);
    }

    private async Task<CourseResponse> CreatePublishedCourseAsync(HttpClient client)
    {
        using var create = await PostWithCsrfAsync(client, Guid.Empty,
            "/api/academics/courses", new
            {
                Code = $"INT-{Guid.NewGuid():N}"[..14],
                Title = "Interaction Course",
                Description = ""
            });
        create.EnsureSuccessStatusCode();
        var course = (await create.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var publish = await PostWithCsrfAsync(client, Guid.Empty,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        return (await publish.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private async Task<ClassResponse> CreateClassAsync(HttpClient client, Guid courseId)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var create = await PostWithCsrfAsync(client, Guid.Empty, "/api/academics/classes", new
        {
            CourseId = courseId,
            Code = $"INT-{Guid.NewGuid():N}"[..14],
            Name = "Interaction Class",
            StartDate = startDate,
            EndDate = startDate.AddMonths(1)
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<ClassResponse>())!;
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, Guid sessionId,
        string suffix, object body)
    {
        var path = sessionId == Guid.Empty
            ? suffix
            : $"/api/classroom/sessions/{sessionId:D}{suffix}";
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

    private Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body) =>
        PostWithCsrfAsync(client, Guid.Empty, path, body);

    private sealed record SessionSetup(
        HttpClient Teacher,
        HttpClient Student,
        LiveSessionResponse Session,
        Guid ClassId,
        Guid TeacherId,
        Guid StudentId,
        Guid SecondStudentId);
}
