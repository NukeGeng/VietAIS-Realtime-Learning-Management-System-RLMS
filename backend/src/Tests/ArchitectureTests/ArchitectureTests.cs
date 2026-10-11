using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using LearningPlatform.BuildingBlocks.Web;
using LearningPlatform.Host;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.ArchitectureTests;

public sealed class ArchitectureTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Repository root was not found.");
        }
    }

    [Fact]
    public void Building_blocks_do_not_reference_business_modules_or_hosts()
    {
        foreach (var project in Directory.EnumerateFiles(Path.Combine(Root, "backend/src/BuildingBlocks"),
                     "*.csproj", SearchOption.AllDirectories))
        {
            foreach (var reference in XDocument.Load(project).Descendants("ProjectReference"))
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!,
                    reference.Attribute("Include")!.Value));
                Assert.DoesNotContain("/Modules/", path);
                Assert.DoesNotContain("/Host/", path);
            }
        }
    }

    [Fact]
    public void Contracts_do_not_reference_runtime_modules_or_persistence_implementations()
    {
        foreach (var project in Directory.EnumerateFiles(Path.Combine(Root, "backend/src/Modules"),
                     "*.Contracts.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(project);
            Assert.Empty(document.Descendants("PackageReference"));
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var path = reference.Attribute("Include")!.Value;
                Assert.True(path.Contains(".Contracts/", StringComparison.Ordinal) ||
                            path.Contains("BuildingBlocks/Core/", StringComparison.Ordinal), path);
            }
        }
    }

    [Fact]
    public void Runtime_modules_retain_assembly_identity_and_do_not_add_cross_module_dependencies()
    {
        var graph = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(
            Path.Combine(Root, "backend/src/Tests/ArchitectureTests/module-dependency-baseline.json")))!;
        foreach (var (module, allowed) in graph)
        {
            var project = Path.Combine(Root, $"backend/src/Modules/{module}/Modules.{module}/Modules.{module}.csproj");
            var document = XDocument.Load(project);
            Assert.Equal($"LearningPlatform.{module}", document.Descendants("AssemblyName").Single().Value);
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!,
                    reference.Attribute("Include")!.Value));
                if (!path.Contains("/Modules/", StringComparison.Ordinal) ||
                    path.Contains(".Contracts/", StringComparison.Ordinal)) continue;
                var target = Directory.GetParent(Path.GetDirectoryName(path)!)!.Name;
                Assert.Contains(target, allowed);
            }
        }
    }

    [Theory]
    [InlineData("LearningPlatform.Identity.Domain.NotificationRequested", "Identity")]
    [InlineData("LearningPlatform.Identity.Domain.ClassNotificationRequested", "Identity")]
    [InlineData("LearningPlatform.Academics.Contracts.EnrollmentChangedForReporting", "Academics")]
    [InlineData("LearningPlatform.Classroom.Attendance.LiveSessionEnded", "Classroom")]
    [InlineData("LearningPlatform.Assessment.Realtime.StudentActivityChanged", "Assessment")]
    [InlineData("LearningPlatform.Learning.Contracts.GradeEntryPublished", "Learning")]
    [InlineData("LearningPlatform.Communication.Domain.AnnouncementPublished", "Communication")]
    public void Persisted_integration_messages_resolve_through_previous_assembly_name(string name, string module)
    {
        var legacy = Type.GetType($"{name}, LearningPlatform.{module}", throwOnError: true);
        var current = Type.GetType($"{name}, LearningPlatform.{module}.Contracts", throwOnError: true);
        Assert.Same(current, legacy);
    }

    [Fact]
    public async Task Migrated_features_preserve_every_http_route_and_authorization_policy()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=architecture;Username=architecture;Password=unused",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Identity:BootstrapToken"] = "unused-architecture-token",
            ["ObjectStorage:Endpoint"] = "http://localhost:9000",
            ["ObjectStorage:Bucket"] = "architecture",
            ["ObjectStorage:AccessKey"] = "unused",
            ["ObjectStorage:SecretKey"] = "unused",
            ["Media:ServerUrl"] = "ws://localhost:7880"
        });
        builder.AddRlmsBackend();
        await using var app = builder.Build();
        foreach (var module in app.Services.GetRequiredService<IReadOnlyList<IModule>>())
            module.MapEndpoints(app);
        // Host-owned routes are covered by integration health/CSRF tests. This check compares every module route.
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        var baseline = JsonSerializer.Deserialize<RouteContract[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "api-contract-baseline.json")))!;
        foreach (var expected in baseline.Where(route => route.Route.StartsWith("/api/", StringComparison.Ordinal) &&
                     route.Route != "/api/identity/csrf"))
        {
            var endpoint = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == expected.Route &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(expected.Method));
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(metadata => metadata.Policy).Where(policy => policy is not null).ToArray();
            Assert.Equal(expected.Policy is null ? [] : new[] { expected.Policy }, policies);
            Assert.Equal(expected.Anonymous, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
        }
    }

    private sealed record RouteContract(string Method, string Route, string? Policy, bool Anonymous);
}
