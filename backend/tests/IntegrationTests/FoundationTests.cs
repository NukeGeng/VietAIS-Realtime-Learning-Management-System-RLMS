using System.Net;
using System.Net.Http.Json;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using LearningPlatform.Identity.Api;
using LearningPlatform.Infrastructure.Storage;
using Marten;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wolverine;

namespace LearningPlatform.IntegrationTests;

public sealed class FoundationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task Anonymous_identity_returns_401_without_redirect()
    {
        using var client = fixture.Client();
        var response = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Valid_cookie_returns_own_identity_and_modified_cookie_is_rejected()
    {
        var expected = await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        var cookie = await fixture.LoginCookieAsync();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var actor = await client.GetFromJsonAsync<AccountResponse>("/api/identity/me");
        Assert.Equal(expected.AdministratorId, actor!.Id);
        Assert.Equal(expected.OrganizationId, actor.OrganizationId);
        Assert.Contains("identity.users.manage", actor.Permissions);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie[..(cookie.IndexOf('=') + 1)] + "corrupted");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
    }

    [Fact]
    public async Task Invalid_organization_cookie_is_rejected()
    {
        using var client = fixture.Client();
        client.DefaultRequestHeaders.Add("Cookie", fixture.Cookie(Guid.NewGuid(), Guid.Empty));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
    }

    [Fact]
    public async Task Unsafe_request_requires_matching_antiforgery_token()
    {
        await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        var authCookie = await fixture.LoginCookieAsync();
        client.DefaultRequestHeaders.Add("Cookie", authCookie);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/probe", null)).StatusCode);

        var csrf = await client.GetAsync("/api/identity/csrf");
        var token = await csrf.Content.ReadFromJsonAsync<CsrfToken>();
        var csrfCookie = csrf.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", authCookie + "; " + csrfCookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/probe", null)).StatusCode);
    }

    [Fact]
    public async Task Database_is_durable_and_http_session_cannot_cross_tenants()
    {
        var organization = await fixture.EnsureBootstrappedAsync();
        var store = fixture.Services.GetRequiredService<IDocumentStore>();
        var tenantA = organization.OrganizationId;
        var tenantB = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using (var write = store.LightweightSession(tenantA.ToString("D")))
        {
            write.Store(new TenantProbe(id, "private"));
            await write.SaveChangesAsync();
        }

        using var client = fixture.Client();
        client.DefaultRequestHeaders.Add("Cookie", await fixture.LoginCookieAsync());
        var saved = await client.GetFromJsonAsync<TenantProbe>($"/api/probe/{id}");
        Assert.Equal("private", saved!.Value);
        client.DefaultRequestHeaders.Add("X-Organization-Id", tenantB.ToString());
        var stillPrivate = await client.GetFromJsonAsync<TenantProbe>($"/api/probe/{id}");
        Assert.Equal("private", stillPrivate!.Value);
        await using var query = store.QuerySession(tenantB.ToString("D"));
        Assert.Empty(await query.Query<TenantProbe>().Where(x => x.Id == id).ToListAsync());
    }

    [Fact]
    public async Task Tenant_context_comes_from_the_authenticated_actor_not_a_request_header()
    {
        var expected = await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        FoundationFixture.SetCookies(client, await fixture.LoginCookieAsync());
        client.DefaultRequestHeaders.Add("X-Organization-Id", Guid.NewGuid().ToString("D"));

        var context = await client.GetFromJsonAsync<TenantContextResponse>("/api/probe/tenant-context");
        Assert.Equal(expected.OrganizationId, context!.OrganizationId);
        Assert.Equal(expected.OrganizationId.ToString("D"), context.TenantId);
    }

    [Fact]
    public async Task Redis_ephemeral_round_trip()
    {
        var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var key = $"rlms:test:{Guid.NewGuid():N}";
        try
        {
            Assert.True(await redis.StringSetAsync(key, "online", TimeSpan.FromMinutes(1)));
            Assert.Equal("online", (string?)await redis.StringGetAsync(key));
            Assert.NotNull(await redis.KeyTimeToLiveAsync(key));
        }
        finally
        {
            await redis.KeyDeleteAsync(key);
        }
    }

    [Fact]
    public async Task Binary_round_trip_uses_object_storage()
    {
        var s3 = fixture.Services.GetRequiredService<IAmazonS3>();
        var bucket = fixture.Services.GetRequiredService<IOptions<ObjectStorageOptions>>().Value.Bucket;
        var key = $"integration-tests/{Guid.NewGuid():N}.txt";
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = "RLMS binary storage",
                ContentType = "text/plain"
            });
            using var response = await s3.GetObjectAsync(bucket, key);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            Assert.Equal("RLMS binary storage", await reader.ReadToEndAsync());
        }
        finally
        {
            await s3.DeleteObjectAsync(bucket, key);
        }
    }

    [Fact]
    public async Task Durable_local_message_reaches_its_handler()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new ProbeMessage(tenant, id));
        var store = fixture.Services.GetRequiredService<IDocumentStore>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var session = store.QuerySession(tenant.ToString("D"));
            var document = await session.LoadAsync<TenantProbe>(id, timeout.Token);
            if (document is not null)
            {
                Assert.Equal("delivered", document.Value);
                break;
            }
            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task SignalR_negotiation_requires_authenticated_organization()
    {
        await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/hubs/probe/negotiate?negotiateVersion=1", null)).StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", await fixture.LoginCookieAsync());
        var response = await client.PostAsync("/hubs/probe/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("connectionToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SignalR_authenticated_connection_can_exchange_messages()
    {
        await fixture.EnsureBootstrappedAsync();
        var authCookie = await fixture.LoginCookieAsync();
        await using var connection = new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/probe", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                options.Headers["Cookie"] = authCookie;
            }).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await connection.StartAsync(timeout.Token);
        Assert.Equal("connected", await connection.InvokeAsync<string>("Echo", "connected", timeout.Token));
        await connection.StopAsync(timeout.Token);
    }

    [Fact]
    public async Task Health_endpoints_do_not_expose_dependency_details_to_anonymous_users()
    {
        await fixture.EnsureBootstrappedAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/health/dependencies")).StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", await fixture.LoginCookieAsync());
        var response = await client.GetAsync("/health/dependencies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private sealed record CsrfToken(string Token);
    private sealed record TenantContextResponse(Guid OrganizationId, string TenantId);
}
