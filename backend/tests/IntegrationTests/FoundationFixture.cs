using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Identity.Api;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Wolverine;
using DomainIdentityConstants = LearningPlatform.Identity.Domain.IdentityConstants;
using DomainIdentityOptions = LearningPlatform.Identity.Domain.IdentityOptions;

namespace LearningPlatform.IntegrationTests;

public sealed class FoundationFixture : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"rlms_tests_{Guid.NewGuid():N}"[..24];

    public FoundationFixture()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            TestConfiguration().GetConnectionString("Postgres") ??
            throw new InvalidOperationException("A local PostgreSQL connection string is required for integration tests."))
        {
            Database = "postgres"
        };
        using var master = new NpgsqlConnection(connection.ConnectionString);
        master.Open();
        using var command = master.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\" OWNER rlms";
        command.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var localSettings = FindRepositoryRoot() is { } root
                ? Path.Combine(root, "backend/src/Api/appsettings.Local.json")
                : null;
            if (localSettings is not null && File.Exists(localSettings))
            {
                configuration.AddJsonFile(localSettings, optional: false, reloadOnChange: false);
            }
        });
        builder.ConfigureServices((context, services) =>
        {
            var connection = new NpgsqlConnectionStringBuilder(
                context.Configuration.GetConnectionString("Postgres"))
            { Database = databaseName }.ConnectionString;
            services.AddSingleton<IStartupFilter, ProbeEndpoints>();
            services.ConfigureMarten(options =>
            {
                options.Connection(connection);
                options.Schema.For<TenantProbe>();
            });
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
            services.ConfigureWolverine(options =>
                options.Discovery.IncludeAssembly(typeof(ProbeMessageHandler).Assembly));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            var connection = new NpgsqlConnectionStringBuilder(
                TestConfiguration().GetConnectionString("Postgres") ??
                throw new InvalidOperationException("A local PostgreSQL connection string is required for integration tests."))
            {
                Database = "postgres"
            };
            using var master = new NpgsqlConnection(connection.ConnectionString);
            master.Open();
            using var command = master.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
            command.ExecuteNonQuery();
        }
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    public string Cookie(Guid userId, Guid organizationId)
    {
        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(AuthenticatedActor.OrganizationClaim, organizationId.ToString())
        ], scheme));
        var options = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        }, scheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    public string BootstrapToken => Services.GetRequiredService<IOptions<DomainIdentityOptions>>().Value.BootstrapToken;

    public async Task<(string Token, string Cookie)> IssueCsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/identity/csrf");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CsrfPayload>();
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var headers)
            ? headers.ToArray()
            : [];
        var cookie = setCookies
            .FirstOrDefault(value => value.StartsWith("__Host-rlms-csrf=", StringComparison.Ordinal))
            ?.Split(';')[0];
        if (cookie is null && client.DefaultRequestHeaders.TryGetValues("Cookie", out var requestCookies))
        {
            cookie = requestCookies
                .SelectMany(value => value.Split(';', StringSplitOptions.TrimEntries))
                .FirstOrDefault(value => value.StartsWith("__Host-rlms-csrf=", StringComparison.Ordinal));
        }
        if (cookie is null)
        {
            throw new InvalidOperationException(
                $"CSRF response did not set a cookie ({(int)response.StatusCode}): " +
                $"{await response.Content.ReadAsStringAsync()} Request cookies: " +
                (client.DefaultRequestHeaders.TryGetValues("Cookie", out var existing)
                    ? string.Join("; ", existing)
                    : "<none>"));
        }
        return (payload!.Token, cookie);
    }

    public static void SetCookies(HttpClient client, params string[] cookies)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
    }

    public async Task<(Guid OrganizationId, Guid AdministratorId)> EnsureBootstrappedAsync()
    {
        var organizationId = DomainIdentityConstants.OrganizationId(IdentityRules.NormalizeCode("RLMS-TEST"));
        var administratorId = DomainIdentityConstants.UserId(organizationId,
            IdentityRules.NormalizeEmail("admin@rlms.test"));
        using var client = Client();
        var csrf = await IssueCsrfAsync(client);
        SetCookies(client, csrf.Cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        client.DefaultRequestHeaders.Add("X-RLMS-Bootstrap-Token", BootstrapToken);
        for (var attempt = 0; attempt < 80; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/identity/bootstrap", new
            {
                OrganizationCode = "RLMS-TEST",
                OrganizationName = "RLMS Integration Test Organization",
                AdminEmail = "admin@rlms.test",
                AdminDisplayName = "Test Administrator",
                AdminPassword = "correct horse battery staple"
            });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BootstrapResponse>();
                return (result!.OrganizationId, result.AdministratorId);
            }

            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Bootstrap failed with {(int)response.StatusCode} ({response.StatusCode}); " +
                    $"configured token length: {BootstrapToken.Length}; response: {body}");
            }

            var store = Services.GetRequiredService<IDocumentStore>();
            await using var tenant = store.QuerySession(organizationId.ToString("D"));
            if (await tenant.LoadAsync<UserAccount>(administratorId) is not null)
            {
                return (organizationId, administratorId);
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The integration bootstrap did not finish creating its administrator.");
    }

    public async Task<string> LoginCookieAsync(
        string email = "admin@rlms.test",
        string password = "correct horse battery staple",
        string organizationCode = "RLMS-TEST")
    {
        await EnsureBootstrappedAsync();
        using var client = Client();
        var csrf = await IssueCsrfAsync(client);
        SetCookies(client, csrf.Cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        using var response = await client.PostAsJsonAsync("/api/identity/login", new
        {
            OrganizationCode = organizationCode,
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-rlms=", StringComparison.Ordinal))
            .Split(';')[0];
    }

    public async Task<(string Code, string Email, Guid OrganizationId, Guid UserId)> CreateTestOrganizationAsync(
        string password = "correct horse battery staple")
    {
        await EnsureBootstrappedAsync();
        var code = IdentityRules.NormalizeCode($"TEST-{Guid.NewGuid():N}"[..14]);
        var organizationId = DomainIdentityConstants.OrganizationId(code);
        var email = IdentityRules.NormalizeEmail($"admin-{Guid.NewGuid():N}@rlms.test");
        var userId = DomainIdentityConstants.UserId(organizationId, email);
        var now = DateTimeOffset.UtcNow;
        var passwordHasher = Services.GetRequiredService<IPasswordHasher<UserAccount>>();
        var store = Services.GetRequiredService<IDocumentStore>();

        await using (var control = store.LightweightSession(DomainIdentityConstants.ControlPlaneTenantId))
        {
            control.Insert(new Organization
            {
                Id = organizationId,
                Code = code,
                NormalizedCode = code,
                Name = "RLMS Integration Test Organization",
                Status = OrganizationStatus.Active,
                CreatedAt = now
            });
            await control.SaveChangesAsync();
        }

        await using (var tenant = store.LightweightSession(organizationId.ToString("D")))
        {
            foreach (var role in PermissionCatalog.BuiltInRoles)
            {
                tenant.Insert(new RoleDefinition
                {
                    Id = DomainIdentityConstants.RoleId(organizationId, role.Key),
                    OrganizationId = organizationId,
                    Name = role.Key,
                    NormalizedName = role.Key,
                    Permissions = role.Value,
                    CreatedAt = now
                });
            }

            tenant.Insert(new UserAccount
            {
                Id = userId,
                OrganizationId = organizationId,
                Email = email,
                NormalizedEmail = email,
                DisplayName = "Integration Test Administrator",
                PasswordHash = passwordHasher.HashPassword(new UserAccount(), password),
                Status = AccountStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                RoleIds = [DomainIdentityConstants.RoleId(organizationId, "ADMIN")],
                CreatedAt = now,
                UpdatedAt = now
            });
            await tenant.SaveChangesAsync();
        }

        return (code, email, organizationId, userId);
    }

    public async Task<(Guid OrganizationId, Guid UserId)> CreateTestAccountAsync(
        string email, string password, string roleName = "STUDENT")
    {
        var bootstrapped = await EnsureBootstrappedAsync();
        var normalizedEmail = IdentityRules.NormalizeEmail(email);
        var normalizedRole = IdentityRules.NormalizeRoleName(roleName);
        if (!PermissionCatalog.BuiltInRoles.ContainsKey(normalizedRole))
        {
            throw new InvalidOperationException($"Unknown integration role '{normalizedRole}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var store = Services.GetRequiredService<IDocumentStore>();
        var passwordHasher = Services.GetRequiredService<IPasswordHasher<UserAccount>>();
        await using var tenant = store.LightweightSession(bootstrapped.OrganizationId.ToString("D"));
        var userId = DomainIdentityConstants.UserId(bootstrapped.OrganizationId, normalizedEmail);
        tenant.Insert(new UserAccount
        {
            Id = userId,
            OrganizationId = bootstrapped.OrganizationId,
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            DisplayName = "Integration Test Account",
            PasswordHash = passwordHasher.HashPassword(new UserAccount(), password),
            Status = AccountStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            RoleIds = [DomainIdentityConstants.RoleId(bootstrapped.OrganizationId, normalizedRole)],
            CreatedAt = now,
            UpdatedAt = now
        });
        await tenant.SaveChangesAsync();
        return (bootstrapped.OrganizationId, userId);
    }

    private sealed record CsrfPayload(string Token);

    private static IConfiguration TestConfiguration()
    {
        var root = FindRepositoryRoot();
        return new ConfigurationBuilder()
            .SetBasePath(root ?? Directory.GetCurrentDirectory())
            .AddJsonFile(root is null ? "missing-appsettings.Local.json" :
                Path.Combine(root, "backend/src/Api/appsettings.Local.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend/src/Api/appsettings.Local.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

// Test-only endpoints exercise the application's real cookie/CSRF/tenant pipeline.
public sealed class ProbeEndpoints : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRouting();
        next(app);
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/api/probe", () => Results.NoContent()).RequireAuthorization();
            endpoints.MapGet("/api/probe/{id:guid}", async (Guid id, IDocumentSession session) =>
            {
                var document = await session.LoadAsync<TenantProbe>(id);
                return document is null ? Results.NotFound() : Results.Ok(document);
            }).RequireAuthorization();
            endpoints.MapHub<ProbeHub>("/hubs/probe").RequireAuthorization();
        });
    };
}

public sealed class ProbeHub : Hub
{
    public string Echo(string value) => value;
}
public sealed record TenantProbe(Guid Id, string Value);
public sealed record ProbeMessage(Guid OrganizationId, Guid Id);

public static class ProbeMessageHandler
{
    public static async Task Handle(ProbeMessage message, IDocumentStore store)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        session.Store(new TenantProbe(message.Id, "delivered"));
        await session.SaveChangesAsync();
    }
}
