using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Communication.Api;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Api;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Reporting.Api;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace LearningPlatform.IntegrationTests;

public sealed class LearningAndCommunicationTests(FoundationFixture fixture)
    : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Student_lesson_progress_is_scoped_to_the_class_and_cannot_regress_after_completion()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, lesson) = await CreateClassWithLessonAsync(administrator);
        var studentEmail = $"lesson-progress-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "lesson progress student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enroll = await PostWithCsrfAsync(administrator,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        var initial = await studentClient.GetFromJsonAsync<CourseProgressResponse>(
            $"/api/learning/classes/{learningClass.Id:D}/course-progress/me");
        Assert.NotNull(initial);
        Assert.Equal(1, initial!.TotalLessons);
        Assert.Equal(0m, initial.CompletionPercent);
        var initialEntry = Assert.Single(initial.Entries);
        Assert.Equal(student.UserId, initialEntry.StudentId);
        Assert.Null(initialEntry.Status);

        using var inProgress = await PostWithCsrfAsync(studentClient,
            $"/api/learning/classes/{learningClass.Id:D}/lessons/{lesson.Id:D}/progress", new
            {
                Status = (int)LessonProgressStatus.InProgress
            });
        Assert.Equal(HttpStatusCode.OK, inProgress.StatusCode);
        Assert.Equal(LessonProgressStatus.InProgress,
            (await inProgress.Content.ReadFromJsonAsync<LessonProgressEntryResponse>())!.Status);

        using var completed = await PostWithCsrfAsync(studentClient,
            $"/api/learning/classes/{learningClass.Id:D}/lessons/{lesson.Id:D}/progress", new
            {
                Status = (int)LessonProgressStatus.Completed
            });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedEntry = await completed.Content.ReadFromJsonAsync<LessonProgressEntryResponse>();
        Assert.Equal(LessonProgressStatus.Completed, completedEntry!.Status);
        Assert.NotNull(completedEntry.CompletedAt);

        using var regression = await PostWithCsrfAsync(studentClient,
            $"/api/learning/classes/{learningClass.Id:D}/lessons/{lesson.Id:D}/progress", new
            {
                Status = (int)LessonProgressStatus.InProgress
            });
        Assert.Equal(HttpStatusCode.Conflict, regression.StatusCode);

        var mine = await studentClient.GetFromJsonAsync<CourseProgressResponse>(
            $"/api/learning/classes/{learningClass.Id:D}/course-progress/me");
        Assert.Equal(100m, mine!.CompletionPercent);
        Assert.Equal(LessonProgressStatus.Completed, Assert.Single(mine.Entries).Status);

        var teacher = await administrator.GetFromJsonAsync<CourseProgressResponse>(
            $"/api/learning/classes/{learningClass.Id:D}/course-progress");
        Assert.NotNull(teacher);
        Assert.Equal(100m, teacher!.CompletionPercent);
        Assert.Equal(student.UserId, Assert.Single(teacher.Entries).StudentId);
    }

    [Fact]
    public async Task Published_lesson_changes_refresh_student_reporting_snapshot()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, lesson) = await CreateClassWithLessonAsync(administrator);
        var studentEmail = $"lesson-report-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "lesson report student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enroll = await PostWithCsrfAsync(administrator,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        using var initialResponse = await studentClient.GetAsync(
            $"/api/reporting/classes/{learningClass.Id:D}/students/me");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        var initial = await initialResponse.Content.ReadFromJsonAsync<StudentReportResponse>();
        Assert.NotNull(initial);
        Assert.Equal(1, initial!.LessonCount);
        var initialVersion = initial.Version;

        using var createLesson = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{lesson.CourseId:D}/modules/{lesson.CourseModuleId:D}/lessons",
            new
            {
                Title = "Second lesson",
                Order = 1,
                ContentSummary = "Second lesson content"
            });
        Assert.Equal(HttpStatusCode.Created, createLesson.StatusCode);
        var secondLesson = await createLesson.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.NotNull(secondLesson);
        Assert.Equal(AcademicContentStatus.Draft, secondLesson!.Status);

        using var publishLesson = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{lesson.CourseId:D}/modules/{lesson.CourseModuleId:D}/lessons/{secondLesson.Id:D}/publish",
            new { });
        Assert.Equal(HttpStatusCode.OK, publishLesson.StatusCode);

        StudentReportResponse? published = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (published is null || published.LessonCount != 2 ||
                   published.Version <= initialVersion)
            {
                using var response = await studentClient.GetAsync(
                    $"/api/reporting/classes/{learningClass.Id:D}/students/me", timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                published = await response.Content.ReadFromJsonAsync<StudentReportResponse>(
                    timeout.Token);
                if (published?.LessonCount == 2 && published.Version > initialVersion)
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }

        Assert.NotNull(published);
        Assert.Equal(2, published!.LessonCount);
        var publishedVersion = published.Version;

        using var archiveLesson = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{lesson.CourseId:D}/modules/{lesson.CourseModuleId:D}/lessons/{secondLesson.Id:D}/archive",
            new { });
        Assert.Equal(HttpStatusCode.OK, archiveLesson.StatusCode);

        StudentReportResponse? archived = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (archived is null || archived.LessonCount != 1 ||
                   archived.Version <= publishedVersion)
            {
                using var response = await studentClient.GetAsync(
                    $"/api/reporting/classes/{learningClass.Id:D}/students/me", timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                archived = await response.Content.ReadFromJsonAsync<StudentReportResponse>(
                    timeout.Token);
                if (archived?.LessonCount == 1 && archived.Version > publishedVersion)
                {
                    break;
                }

                await Task.Delay(100, timeout.Token);
            }
        }

        Assert.NotNull(archived);
        Assert.Equal(1, archived!.LessonCount);
        Assert.True(archived.Version > publishedVersion);
    }

    [Fact]
    public async Task Published_resources_are_visible_to_enrolled_students_and_files_use_tenant_storage()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, lesson) = await CreateClassWithLessonAsync(administrator);

        using var create = await PostWithCsrfAsync(administrator,
            $"/api/learning/classes/{learningClass.Id:D}/resources", new
            {
                LessonId = lesson.Id,
                Title = "Lesson video",
                Description = "Recorded explanation",
                Type = LearningResourceType.Video,
                ExternalUrl = "https://cdn.example.test/lesson-video.mp4",
                DurationSeconds = 600
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var draft = await create.Content.ReadFromJsonAsync<LearningResourceResponse>();
        Assert.NotNull(draft);
        Assert.Equal(LearningResourceStatus.Draft, draft.Status);

        var studentEmail = $"resource-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "resource student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enroll = await PostWithCsrfAsync(administrator,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        var beforePublish = await studentClient.GetFromJsonAsync<LearningResourceResponse[]>(
            $"/api/learning/classes/{learningClass.Id:D}/resources");
        Assert.Empty(beforePublish!);

        using var publish = await PostWithCsrfAsync(administrator,
            $"/api/learning/resources/{draft!.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var resources = await studentClient.GetFromJsonAsync<LearningResourceResponse[]>(
            $"/api/learning/classes/{learningClass.Id:D}/resources");
        var visible = Assert.Single(resources!);
        Assert.Equal(LearningResourceStatus.Published, visible.Status);
        Assert.Equal("https://cdn.example.test/lesson-video.mp4", visible.AccessUrl);

        using var uploaded = await PostMultipartWithCsrfAsync(administrator,
            $"/api/learning/classes/{learningClass.Id:D}/resources/upload",
            new Dictionary<string, string>
            {
                ["lessonId"] = lesson.Id.ToString("D"),
                ["title"] = "Handout",
                ["description"] = "PDF handout",
                ["type"] = LearningResourceType.Document.ToString()
            }, "file", "handout.pdf", "application/pdf", [37, 80, 68, 70]);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var uploadedResource = await uploaded.Content.ReadFromJsonAsync<LearningResourceResponse>();
        Assert.NotNull(uploadedResource);
        Assert.Null(uploadedResource.ExternalUrl);

        var store = fixture.Services.GetRequiredService<Marten.IDocumentStore>();
        await using var tenant = store.QuerySession(student.OrganizationId.ToString("D"));
        var stored = await tenant.LoadAsync<LearningResource>(uploadedResource!.Id);
        Assert.NotNull(stored);
        Assert.StartsWith($"{student.OrganizationId:D}/resources/", stored!.StorageKey,
            StringComparison.Ordinal);

        var otherOrganization = await fixture.CreateTestOrganizationAsync();
        using var otherClient = fixture.Client();
        FoundationFixture.SetCookies(otherClient,
            await fixture.LoginCookieAsync(otherOrganization.Email, "correct horse battery staple",
                otherOrganization.Code));
        using var crossTenant = await otherClient.GetAsync(
            $"/api/learning/resources/{draft.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Published_announcement_fans_out_an_in_app_notification_and_can_be_marked_read()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, _) = await CreateClassWithLessonAsync(administrator);
        var studentEmail = $"announcement-student-{Guid.NewGuid():N}@rlms.test";
        var studentPassword = "announcement student password";
        var student = await fixture.CreateTestAccountAsync(studentEmail, studentPassword, "STUDENT");
        using var enroll = await PostWithCsrfAsync(administrator,
            $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
            {
                StudentId = student.UserId
            });
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);

        var invalid = await PostWithCsrfAsync(administrator,
            $"/api/communication/classes/{learningClass.Id:D}/announcements", new
            {
                Title = "",
                Content = "No title"
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var create = await PostWithCsrfAsync(administrator,
            $"/api/communication/classes/{learningClass.Id:D}/announcements", new
            {
                Title = "Welcome",
                Content = "The first live session is tomorrow."
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var announcement = await create.Content.ReadFromJsonAsync<AnnouncementResponse>();
        Assert.NotNull(announcement);

        using var studentClient = fixture.Client();
        FoundationFixture.SetCookies(studentClient,
            await fixture.LoginCookieAsync(studentEmail, studentPassword));
        var drafts = await studentClient.GetFromJsonAsync<AnnouncementResponse[]>(
            $"/api/communication/classes/{learningClass.Id:D}/announcements");
        Assert.Empty(drafts!);
        using var hidden = await studentClient.GetAsync(
            $"/api/communication/announcements/{announcement!.Id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);

        using var publish = await PostWithCsrfAsync(administrator,
            $"/api/communication/announcements/{announcement.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        NotificationResponse[] notifications = [];
        for (var attempt = 0; attempt < 30; attempt++)
        {
            notifications = await studentClient.GetFromJsonAsync<NotificationResponse[]>(
                "/api/communication/notifications?unreadOnly=true") ?? [];
            if (notifications.Length > 0)
            {
                break;
            }

            await Task.Delay(100);
        }

        var notification = Assert.Single(notifications);
        Assert.Equal("announcement", notification.Type);
        Assert.Equal(announcement.Id, notification.SourceId);
        using var markRead = await PostWithCsrfAsync(studentClient,
            $"/api/communication/notifications/{notification.Id:D}/read", new { });
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);
        var unread = await studentClient.GetFromJsonAsync<NotificationResponse[]>(
            "/api/communication/notifications?unreadOnly=true");
        Assert.Empty(unread!);
    }

    [Fact]
    public async Task Class_notification_without_source_is_fanned_out_to_each_active_student()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, _) = await CreateClassWithLessonAsync(administrator);
        var first = await fixture.CreateTestAccountAsync(
            $"class-notification-first-{Guid.NewGuid():N}@rlms.test",
            "class notification first password");
        var second = await fixture.CreateTestAccountAsync(
            $"class-notification-second-{Guid.NewGuid():N}@rlms.test",
            "class notification second password");

        foreach (var student in new[] { first, second })
        {
            using var enroll = await PostWithCsrfAsync(administrator,
                $"/api/academics/classes/{learningClass.Id:D}/enrollments", new
                {
                    StudentId = student.UserId
                });
            Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);
        }

        var eventId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(
                new ClassNotificationRequested(eventId, first.OrganizationId, learningClass.Id,
                    "class-test", "Class update", "A class update", null, DateTimeOffset.UtcNow));
        }

        var store = fixture.Services.GetRequiredService<IDocumentStore>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Notification[] notifications;
        do
        {
            await using var query = store.QuerySession(first.OrganizationId.ToString("D"));
            notifications = (await query.Query<Notification>()
                .Where(value => value.Type == "class-test" && value.SourceId == null)
                .ToListAsync(timeout.Token)).ToArray();
            if (notifications.Length < 2)
            {
                await Task.Delay(50, timeout.Token);
            }
        } while (notifications.Length < 2);

        Assert.Equal(new[] { first.UserId, second.UserId }.OrderBy(value => value),
            notifications.Select(value => value.UserId).OrderBy(value => value));
    }

    [Fact]
    public async Task An_unassigned_teacher_cannot_manage_class_resources_or_announcements()
    {
        using var administrator = fixture.Client();
        FoundationFixture.SetCookies(administrator, await fixture.LoginCookieAsync());
        var (learningClass, lesson) = await CreateClassWithLessonAsync(administrator);
        var teacherEmail = $"resource-teacher-{Guid.NewGuid():N}@rlms.test";
        var teacherPassword = "resource teacher password";
        await fixture.CreateTestAccountAsync(teacherEmail, teacherPassword, "TEACHER");
        using var teacher = fixture.Client();
        FoundationFixture.SetCookies(teacher,
            await fixture.LoginCookieAsync(teacherEmail, teacherPassword));

        using var resource = await PostWithCsrfAsync(teacher,
            $"/api/learning/classes/{learningClass.Id:D}/resources", new
            {
                LessonId = lesson.Id,
                Title = "Unauthorized",
                Description = "",
                Type = LearningResourceType.Document,
                ExternalUrl = "https://cdn.example.test/unauthorized.pdf"
            });
        Assert.Equal(HttpStatusCode.Forbidden, resource.StatusCode);
        using var announcement = await PostWithCsrfAsync(teacher,
            $"/api/communication/classes/{learningClass.Id:D}/announcements", new
            {
                Title = "Unauthorized",
                Content = "No access"
            });
        Assert.Equal(HttpStatusCode.Forbidden, announcement.StatusCode);
    }

    private async Task<(ClassResponse Class, LessonResponse Lesson)> CreateClassWithLessonAsync(
        HttpClient administrator)
    {
        using var courseResponse = await PostWithCsrfAsync(administrator, "/api/academics/courses", new
        {
            Code = $"RESOURCE-{Guid.NewGuid():N}"[..16],
            Title = "Resource course",
            Description = ""
        });
        courseResponse.EnsureSuccessStatusCode();
        var course = (await courseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var coursePublish = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        coursePublish.EnsureSuccessStatusCode();
        using var moduleResponse = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{course.Id:D}/modules", new
            {
                Title = "Module",
                Order = 0
            });
        moduleResponse.EnsureSuccessStatusCode();
        var module = (await moduleResponse.Content.ReadFromJsonAsync<ModuleResponse>())!;
        using var lessonResponse = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons", new
            {
                Title = "Lesson",
                Order = 0,
                ContentSummary = "Lesson content"
            });
        lessonResponse.EnsureSuccessStatusCode();
        var lesson = (await lessonResponse.Content.ReadFromJsonAsync<LessonResponse>())!;
        using var publishStructure = await PostWithCsrfAsync(administrator,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        publishStructure.EnsureSuccessStatusCode();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        using var classResponse = await PostWithCsrfAsync(administrator, "/api/academics/classes", new
        {
            CourseId = course.Id,
            Code = $"RESCLASS-{Guid.NewGuid():N}"[..16],
            Name = "Resource class",
            StartDate = start,
            EndDate = start.AddMonths(1)
        });
        classResponse.EnsureSuccessStatusCode();
        return ((await classResponse.Content.ReadFromJsonAsync<ClassResponse>())!, lesson);
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path,
        object body)
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

    private async Task<HttpResponseMessage> PostMultipartWithCsrfAsync(HttpClient client,
        string path, IReadOnlyDictionary<string, string> fields, string fileField, string fileName,
        string contentType, byte[] bytes)
    {
        var authCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(cookie => cookie.StartsWith("__Host-rlms=", StringComparison.Ordinal));
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        using var form = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            form.Add(new StringContent(field.Value), field.Key);
        }

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, fileField, fileName);
        return await client.PostAsync(path, form);
    }
}
