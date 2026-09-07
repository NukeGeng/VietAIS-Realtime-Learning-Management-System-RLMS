using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Identity.Api;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.IntegrationTests;

public sealed class IdentityTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Bootstrap_is_one_time_and_requires_the_configured_secret()
    {
        await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, csrf.Cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        client.DefaultRequestHeaders.Add("X-RLMS-BOOTSTRAP-TOKEN", "wrong-token");
        using var denied = await client.PostAsJsonAsync("/api/identity/bootstrap", new
        {
            OrganizationCode = "OTHER",
            OrganizationName = "Should Not Be Created",
            AdminEmail = "other@rlms.test",
            AdminDisplayName = "Other",
            AdminPassword = "correct horse battery staple"
        });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        client.DefaultRequestHeaders.Remove("X-RLMS-BOOTSTRAP-TOKEN");
        client.DefaultRequestHeaders.Add("X-RLMS-BOOTSTRAP-TOKEN", fixture.BootstrapToken);
        using var duplicate = await client.PostAsJsonAsync("/api/identity/bootstrap", new
        {
            OrganizationCode = "RLMS-TEST",
            OrganizationName = "Duplicate",
            AdminEmail = "another@rlms.test",
            AdminDisplayName = "Duplicate",
            AdminPassword = "correct horse battery staple"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_returns_a_cookie_and_me_contains_only_the_authenticated_account()
    {
        var expected = await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        var authCookie = await LoginAsync(client, "admin@rlms.test", "correct horse battery staple");
        FoundationFixture.SetCookies(client, authCookie);
        using var me = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var account = await me.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(expected.AdministratorId, account!.Id);
        Assert.Equal(expected.OrganizationId, account.OrganizationId);
        Assert.Contains(PermissionCatalog.ManageUsers, account.Permissions);

        using var wrongClient = fixture.Client();
        var wrong = await LoginResponseAsync(wrongClient, "RLMS-TEST", "admin@rlms.test", "wrong password");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_an_account_but_a_teacher_cannot_manage_accounts()
    {
        await fixture.EnsureBootstrappedAsync();
        using var adminClient = fixture.Client();
        var adminCookie = await LoginAsync(adminClient, "admin@rlms.test", "correct horse battery staple");
        var teacherRequest = new
        {
            Email = "teacher@rlms.test",
            DisplayName = "Test Teacher",
            Password = "teacher password 123",
            Roles = new[] { "TEACHER" }
        };
        using var create = await PostWithCsrfAsync(adminClient, adminCookie, "/api/identity/users", teacherRequest);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var teacher = await create.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Contains("academics.classes.manage", teacher!.Permissions);
        Assert.DoesNotContain(PermissionCatalog.ManageUsers, teacher.Permissions);

        using var duplicate = await PostWithCsrfAsync(adminClient, adminCookie, "/api/identity/users", teacherRequest);
        Assert.True(duplicate.StatusCode == HttpStatusCode.Conflict,
            $"Expected conflict, got {(int)duplicate.StatusCode}: {await duplicate.Content.ReadAsStringAsync()}");

        using var teacherClient = fixture.Client();
        var teacherCookie = await LoginAsync(teacherClient, "teacher@rlms.test", "teacher password 123");
        FoundationFixture.SetCookies(teacherClient, teacherCookie);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.GetAsync("/api/identity/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.GetAsync("/api/identity/roles")).StatusCode);

        using var update = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Put,
            $"/api/identity/users/{teacher!.Id:D}", new
            {
                DisplayName = "Updated Teacher",
                Password = "updated teacher password",
                Roles = new[] { "TEACHER" }
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var disable = await PostWithCsrfAsync(adminClient, adminCookie,
            $"/api/identity/users/{teacher.Id:D}/disable", new { });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        using var disabledLogin = fixture.Client();
        using var disabledResponse = await LoginResponseAsync(disabledLogin, "RLMS-TEST",
            "teacher@rlms.test", "updated teacher password");
        Assert.Equal(HttpStatusCode.Unauthorized, disabledResponse.StatusCode);

        using var enable = await PostWithCsrfAsync(adminClient, adminCookie,
            $"/api/identity/users/{teacher.Id:D}/enable", new { });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
    }

    [Fact]
    public async Task Admin_can_manage_custom_roles_without_escalating_permissions()
    {
        await fixture.EnsureBootstrappedAsync();
        using var adminClient = fixture.Client();
        var adminCookie = await LoginAsync(adminClient, "admin@rlms.test",
            "correct horse battery staple");

        using var invalid = await PostWithCsrfAsync(adminClient, adminCookie, "/api/identity/roles",
            new
            {
                Name = "Content Reviewer",
                Permissions = new[] { "identity.permission.unknown" }
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var create = await PostWithCsrfAsync(adminClient, adminCookie, "/api/identity/roles",
            new
            {
                Name = "Content Reviewer",
                Permissions = new[] { PermissionCatalog.ReadReports }
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var role = await create.Content.ReadFromJsonAsync<RoleResponse>();
        Assert.NotNull(role);
        Assert.Contains(PermissionCatalog.ReadReports, role.Permissions);

        using var rename = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Put,
            $"/api/identity/roles/{role!.Id:D}", new
            {
                Name = "Content Moderator",
                Permissions = new[] { PermissionCatalog.ReadReports, PermissionCatalog.ReadNotifications }
            });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var renamed = await rename.Content.ReadFromJsonAsync<RoleResponse>();
        Assert.Equal("CONTENT MODERATOR", renamed!.Name);

        using var protectedBuiltIn = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Put,
            $"/api/identity/roles/{IdentityConstants.RoleId(
                (await fixture.EnsureBootstrappedAsync()).OrganizationId, "ADMIN"):D}", new
                {
                    Name = "Administrator Copy",
                    Permissions = new[] { PermissionCatalog.ManageUsers }
                });
        Assert.Equal(HttpStatusCode.Conflict, protectedBuiltIn.StatusCode);

        var userEmail = $"custom-role-{Guid.NewGuid():N}@rlms.test";
        using var createUser = await PostWithCsrfAsync(adminClient, adminCookie, "/api/identity/users",
            new
            {
                Email = userEmail,
                DisplayName = "Custom Role User",
                Password = "custom role user password",
                Roles = new[] { renamed.Name }
            });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        var user = await createUser.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Contains(PermissionCatalog.ReadNotifications, user!.Permissions);

        using var assignedDelete = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Delete,
            $"/api/identity/roles/{renamed.Id:D}", new { });
        Assert.Equal(HttpStatusCode.Conflict, assignedDelete.StatusCode);

        using var moveUser = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Put,
            $"/api/identity/users/{user.Id:D}", new
            {
                DisplayName = (string?)null,
                Password = (string?)null,
                Roles = new[] { "STUDENT" }
            });
        Assert.Equal(HttpStatusCode.OK, moveUser.StatusCode);

        using var delete = await SendWithCsrfAsync(adminClient, adminCookie, HttpMethod.Delete,
            $"/api/identity/roles/{renamed.Id:D}", new { });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Disabled_account_cannot_use_an_existing_cookie()
    {
        var password = "disabled account password";
        var email = $"disabled-{Guid.NewGuid():N}@rlms.test";
        var testAccount = await fixture.CreateTestAccountAsync(email, password);
        using var client = fixture.Client();
        var authCookie = await LoginAsync(client, email, password);
        var store = fixture.Services.GetRequiredService<Marten.IDocumentStore>();
        await using (var session = store.LightweightSession(testAccount.OrganizationId.ToString("D")))
        {
            var account = await session.LoadAsync<UserAccount>(testAccount.UserId);
            account!.Status = AccountStatus.Disabled;
            account.SecurityStamp = "revoked";
            session.Update(account);
            await session.SaveChangesAsync();
        }

        FoundationFixture.SetCookies(client, authCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
    }

    [Fact]
    public async Task Suspended_organization_cannot_use_an_existing_cookie()
    {
        var password = "suspended organization password";
        var testOrganization = await fixture.CreateTestOrganizationAsync(password);
        using var client = fixture.Client();
        var authCookie = await LoginAsync(client, testOrganization.Email, password, testOrganization.Code);
        var store = fixture.Services.GetRequiredService<Marten.IDocumentStore>();
        await using (var session = store.LightweightSession(IdentityConstants.ControlPlaneTenantId))
        {
            var organization = await session.LoadAsync<Organization>(testOrganization.OrganizationId);
            organization!.Status = OrganizationStatus.Suspended;
            session.Update(organization);
            await session.SaveChangesAsync();
        }

        FoundationFixture.SetCookies(client, authCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
    }

    private async Task<string> LoginAsync(
        HttpClient client, string email, string password, string organizationCode = "RLMS-TEST")
    {
        using var response = await LoginResponseAsync(client, organizationCode, email, password);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-rlms=", StringComparison.Ordinal)).Split(';')[0];
    }

    private async Task<HttpResponseMessage> LoginResponseAsync(
        HttpClient client, string organizationCode, string email, string password)
    {
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.PostAsJsonAsync("/api/identity/login", new
        {
            OrganizationCode = organizationCode,
            Email = email,
            Password = password
        });
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string authCookie, string path, object body)
    {
        return await SendWithCsrfAsync(client, authCookie, HttpMethod.Post, path, body);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(HttpClient client, string authCookie,
        HttpMethod method, string path, object body)
    {
        FoundationFixture.SetCookies(client, authCookie);
        var csrf = await fixture.IssueCsrfAsync(client);
        FoundationFixture.SetCookies(client, authCookie, csrf.Cookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        return await client.SendAsync(request);
    }
}
