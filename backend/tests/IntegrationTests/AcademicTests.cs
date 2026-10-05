using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Academics.Api;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Domain;

namespace LearningPlatform.IntegrationTests;

public sealed class AcademicTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Teacher_can_create_publish_and_read_course_structure()
    {
        using var client = await AuthenticatedClientAsync();
        using var create = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = "BACKEND-101",
            Title = "Backend Foundations",
            Description = "Build reliable web services."
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var course = await create.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal(CourseStatus.Draft, course.Status);

        using var duplicate = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = "backend-101",
            Title = "Duplicate",
            Description = ""
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var module = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules", new
            {
                Title = "HTTP APIs",
                Order = 0
            });
        Assert.Equal(HttpStatusCode.Created, module.StatusCode);
        var createdModule = await module.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.NotNull(createdModule);

        using var lesson = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{createdModule.Id:D}/lessons", new
            {
                Title = "Request lifecycle",
                Order = 0,
                ContentSummary = "Routing, middleware and endpoints."
            });
        Assert.Equal(HttpStatusCode.Created, lesson.StatusCode);

        using var publish = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal(CourseStatus.Published, published!.Status);

        var detail = await client.GetFromJsonAsync<CourseDetailResponse>(
            $"/api/academics/courses/{course.Id:D}");
        Assert.Equal(CourseStatus.Published, detail!.Course.Status);
        Assert.Single(detail.Modules);
        Assert.Single(detail.Lessons);
    }

    [Fact]
    public async Task Invalid_course_input_is_rejected_and_students_only_see_enrolled_courses()
    {
        using var teacher = await AuthenticatedClientAsync();
        using var invalid = await PostWithCsrfAsync(teacher, "/api/academics/courses", new
        {
            Code = "bad code",
            Title = "",
            Description = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var password = "student course password";
        var email = $"student-{Guid.NewGuid():N}@rlms.test";
        await fixture.CreateTestAccountAsync(email, password, "STUDENT");
        var studentCookie = await fixture.LoginCookieAsync(email, password);
        using var student = fixture.Client();
        FoundationFixture.SetCookies(student, studentCookie);
        using var courses = await student.GetAsync("/api/academics/courses");
        Assert.Equal(HttpStatusCode.OK, courses.StatusCode);
        Assert.Empty(await courses.Content.ReadFromJsonAsync<CourseResponse[]>() ?? []);
    }

    [Fact]
    public async Task Course_listing_handles_extreme_page_numbers_without_overflow()
    {
        using var client = await AuthenticatedClientAsync();

        using var courses = await client.GetAsync(
            "/api/academics/courses?page=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, courses.StatusCode);
        Assert.Empty(await courses.Content.ReadFromJsonAsync<CourseResponse[]>() ?? []);
    }

    [Fact]
    public async Task Course_content_can_be_updated_and_archived_with_parent_invariants()
    {
        using var client = await AuthenticatedClientAsync();
        using var create = await PostWithCsrfAsync(client, "/api/academics/courses", new
        {
            Code = $"CNT-{Guid.NewGuid():N}"[..14],
            Title = "Content management",
            Description = "Course content lifecycle."
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var course = await create.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);

        using var moduleResponse = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules", new { Title = "Original module", Order = 0 });
        var module = await moduleResponse.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal(HttpStatusCode.Created, moduleResponse.StatusCode);
        Assert.NotNull(module);

        using var secondModuleResponse = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules", new { Title = "Second module", Order = 1 });
        var secondModule = await secondModuleResponse.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal(HttpStatusCode.Created, secondModuleResponse.StatusCode);
        Assert.NotNull(secondModule);

        using var lessonResponse = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons", new
            {
                Title = "Original lesson",
                Order = 0,
                ContentSummary = "Original summary."
            });
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        Assert.NotNull(lesson);

        using var secondLessonResponse = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons", new
            {
                Title = "Second lesson",
                Order = 1,
                ContentSummary = "Second summary."
            });
        var secondLesson = await secondLessonResponse.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.Equal(HttpStatusCode.Created, secondLessonResponse.StatusCode);
        Assert.NotNull(secondLesson);

        using var updateModule = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}", new
            {
                Title = "Updated module",
                Order = 0
            });
        Assert.Equal(HttpStatusCode.OK, updateModule.StatusCode);
        Assert.Equal("Updated module",
            (await updateModule.Content.ReadFromJsonAsync<ModuleResponse>())!.Title);

        using var duplicateModuleOrder = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{secondModule.Id:D}", new
            {
                Title = "Second module",
                Order = 0
            });
        Assert.Equal(HttpStatusCode.Conflict, duplicateModuleOrder.StatusCode);

        using var updateLesson = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons/{lesson.Id:D}", new
            {
                Title = "Updated lesson",
                Order = 0,
                ContentSummary = "Updated summary."
            });
        Assert.Equal(HttpStatusCode.OK, updateLesson.StatusCode);
        var updatedLesson = await updateLesson.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.Equal("Updated lesson", updatedLesson!.Title);
        Assert.Equal("Updated summary.", updatedLesson.ContentSummary);

        using var duplicateLessonOrder = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons/{secondLesson.Id:D}", new
            {
                Title = "Second lesson",
                Order = 0,
                ContentSummary = "Second summary."
            });
        Assert.Equal(HttpStatusCode.Conflict, duplicateLessonOrder.StatusCode);

        using var archiveLesson = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons/{lesson.Id:D}/archive",
            new { });
        Assert.Equal(HttpStatusCode.OK, archiveLesson.StatusCode);
        Assert.Equal(AcademicContentStatus.Archived,
            (await archiveLesson.Content.ReadFromJsonAsync<LessonResponse>())!.Status);

        using var updateArchivedLesson = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/lessons/{lesson.Id:D}", new
            {
                Title = "Should fail",
                Order = 0,
                ContentSummary = "Should fail."
            });
        Assert.Equal(HttpStatusCode.Conflict, updateArchivedLesson.StatusCode);

        using var archiveModule = await PostWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}/archive", new { });
        Assert.Equal(HttpStatusCode.OK, archiveModule.StatusCode);
        Assert.Equal(AcademicContentStatus.Archived,
            (await archiveModule.Content.ReadFromJsonAsync<ModuleResponse>())!.Status);

        var detail = await client.GetFromJsonAsync<CourseDetailResponse>(
            $"/api/academics/courses/{course.Id:D}");
        var archivedModule = Assert.Single(detail!.Modules, value => value.Id == module.Id);
        Assert.Equal(AcademicContentStatus.Archived, archivedModule.Status);
        Assert.All(detail.Lessons.Where(value => value.CourseModuleId == module.Id), value =>
            Assert.Equal(AcademicContentStatus.Archived, value.Status));

        using var updateArchivedModule = await PutWithCsrfAsync(client,
            $"/api/academics/courses/{course.Id:D}/modules/{module.Id:D}", new
            {
                Title = "Should fail",
                Order = 0
            });
        Assert.Equal(HttpStatusCode.Conflict, updateArchivedModule.StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Client();
        var cookie = await fixture.LoginCookieAsync();
        FoundationFixture.SetCookies(client, cookie);
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
}
