using System.Security.Claims;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

namespace LearningPlatform.IntegrationTests;

public sealed class FoundationFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices((context, services) =>
        {
            var connection = new NpgsqlConnectionStringBuilder(
                context.Configuration.GetConnectionString("Postgres"))
            { Database = "rlms_tests" }.ConnectionString;
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
