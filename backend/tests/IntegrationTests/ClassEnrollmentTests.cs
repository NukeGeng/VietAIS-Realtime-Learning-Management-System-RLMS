using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Communication.Api;

namespace LearningPlatform.IntegrationTests;

public sealed class ClassEnrollmentTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Teacher_can_create_class_assign_teacher_enroll_student_and_manage_schedule()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher, await fixture.LoginCookieAsync());
        var course = await CreatePublishedCourseAsync(teacher);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var endDate = startDate.AddMonths(3);

        using var createClass = await PostWithCsrfAsync(teacher, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = $"CLS-{Guid.NewGuid():N}"[..12],
            Name = "Backend Cohort",
            StartDate = startDate,
            EndDate = endDate
        });
        Assert.Equal(HttpStatusCode.Created, createClass.StatusCode);
        var learningClass = await createClass.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(learningClass);
        Assert.Equal(LearningClassStatus.Preparing, learningClass.Status);

        using var activate = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/status", new
            {
                Status = (int)LearningClassStatus.Active
            });
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        learningClass = await activate.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.Equal(LearningClassStatus.Active, learningClass!.Status);

        using var assign = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/teachers", new
            {
                TeacherId = organization.AdministratorId
            });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);

        var studentPassword = "class student password";
        var studentEmail = $"class-student-{Guid.NewGuid():N}@rlms.test";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enroll = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);

        var scheduleStart = DateTimeOffset.UtcNow.AddDays(1);
        using var schedule = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/schedules", new
            {
                TeacherId = organization.AdministratorId,
                StartsAt = scheduleStart,
                EndsAt = scheduleStart.AddHours(2),
                Recurrence = "FREQ=WEEKLY;BYDAY=MO"
            });
        Assert.Equal(HttpStatusCode.Created, schedule.StatusCode);
        var createdSchedule = await schedule.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(createdSchedule);

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        var classes = await studentClient.GetFromJsonAsync<ClassResponse[]>("/api/academics/classes");
        var enrolledClass = Assert.Single(classes!);
        Assert.Equal(learningClass.Id, enrolledClass.Id);
        var enrollments = await studentClient.GetFromJsonAsync<EnrollmentResponse[]>(
            $"/api/academics/classes/{learningClass.Id:D}/enrollments");
        var enrollment = Assert.Single(enrollments!);
        Assert.Equal(student.UserId, enrollment.StudentId);
        var personalSchedule = await studentClient.GetFromJsonAsync<ScheduleResponse[]>(
            "/api/academics/schedules/me");
        var studentSchedule = Assert.Single(personalSchedule!);
        Assert.Equal(createdSchedule.Id, studentSchedule.Id);
        NotificationResponse[] notifications = [];
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (!notifications.Any(notification =>
                       notification.Type == "schedule-created" &&
                       notification.SourceId == createdSchedule.Id))
            {
                notifications = await studentClient.GetFromJsonAsync<NotificationResponse[]>(
                    "/api/communication/notifications?unreadOnly=true", timeout.Token) ?? [];
                if (notifications.Any(notification =>
                        notification.Type == "schedule-created" &&
                        notification.SourceId == createdSchedule.Id))
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }
        Assert.Contains(notifications, notification => notification.Type == "schedule-created" &&
            notification.SourceId == createdSchedule.Id);

        var rescheduleStart = scheduleStart.AddDays(1);
        using var rescheduled = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/schedules/{createdSchedule.Id:D}/reschedule",
            new
            {
                StartsAt = rescheduleStart,
                EndsAt = rescheduleStart.AddHours(1),
                Recurrence = (string?)null
            });
        Assert.Equal(HttpStatusCode.OK, rescheduled.StatusCode);
        var rescheduledSchedule = await rescheduled.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.Equal(ScheduleStatus.Rescheduled, rescheduledSchedule!.Status);

        using var cancelled = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/schedules/{createdSchedule.Id:D}/cancel",
            new { });
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
    }

    [Fact]
    public async Task Class_and_schedule_invariants_are_enforced()
    {
        using var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher, await fixture.LoginCookieAsync());
        var course = await CreatePublishedCourseAsync(teacher);
        using var invalidClass = await PostWithCsrfAsync(teacher, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = "CLASS-INVALID",
            Name = "Bad dates",
            StartDate = "2026-09-10",
            EndDate = "2026-09-09"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidClass.StatusCode);

        var classResponse = await PostWithCsrfAsync(teacher, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = $"CLS-{Guid.NewGuid():N}"[..12],
            Name = "Schedule Invariants",
            StartDate = "2026-09-09",
            EndDate = "2026-09-10"
        });
        var learningClass = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        using var unassignedSchedule = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass!.Id:D}/schedules", new
            {
                TeacherId = Guid.NewGuid(),
                StartsAt = start,
                EndsAt = start.AddHours(1),
                Recurrence = (string?)null
            });
        Assert.Equal(HttpStatusCode.BadRequest, unassignedSchedule.StatusCode);

        var studentEmail = $"class-denied-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "class denied password";
        await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var student = fixture.Client();
        FoundationFixture.SetCookies(student,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        using var denied = await PostWithCsrfAsync(student, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = "STUDENT-CLASS",
            Name = "Denied",
            StartDate = "2026-09-09",
            EndDate = "2026-09-10"
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Teacher_class_management_is_limited_to_created_or_assigned_classes()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var course = await CreatePublishedCourseAsync(administrator);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var created = await PostWithCsrfAsync(administrator, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = $"CLS-{Guid.NewGuid():N}"[..12],
            Name = "Scoped class",
            StartDate = startDate,
            EndDate = startDate.AddMonths(1)
        });
        created.EnsureSuccessStatusCode();
        var learningClass = (await created.Content.ReadFromJsonAsync<ClassResponse>())!;

        var teacherEmail = $"scoped-teacher-{Guid.NewGuid():N}@rlms.test";
        var teacherPassword = "scoped teacher password";
        await fixture.CreateTestAccountAsync(teacherEmail, teacherPassword, "TEACHER");
        using var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher,
            await fixture.LoginCookieAsync(teacherEmail, teacherPassword));

        using var classRead = await teacher.GetAsync($"/api/academics/classes/{learningClass.Id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, classRead.StatusCode);
        var visibleClasses = await teacher.GetFromJsonAsync<ClassResponse[]>("/api/academics/classes");
        Assert.Empty(visibleClasses!);

        using var assign = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/teachers", new
            {
                TeacherId = organization.AdministratorId
            });
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);

        var student = await fixture.CreateTestAccountAsync(
            $"scoped-student-{Guid.NewGuid():N}@rlms.test", "scoped student password", "STUDENT");
        using var enroll = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Forbidden, enroll.StatusCode);

        var scheduleStart = DateTimeOffset.UtcNow.AddDays(1);
        using var schedule = await PostWithCsrfAsync(teacher,
            $"/api/academics/classes/{learningClass.Id:D}/schedules", new
            {
                TeacherId = organization.AdministratorId,
                StartsAt = scheduleStart,
                EndsAt = scheduleStart.AddHours(1),
                Recurrence = (string?)null
            });
        Assert.Equal(HttpStatusCode.Forbidden, schedule.StatusCode);
    }

    private async Task<CourseResponse> CreatePublishedCourseAsync(HttpClient client)
    {
        using var create = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = $"COURSE-{Guid.NewGuid():N}"[..16],
            Title = "Class Course",
            Description = ""
        });
        create.EnsureSuccessStatusCode();
        var course = (await create.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var publish = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        publish.EnsureSuccessStatusCode();
        return (await publish.Content.ReadFromJsonAsync<CourseResponse>())!;
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
