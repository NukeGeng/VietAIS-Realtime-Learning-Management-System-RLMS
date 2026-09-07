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
}
