using LearningPlatform.BuildingBlocks.Web;
using LearningPlatform.BuildingBlocks.Persistence;
using LearningPlatform.BuildingBlocks.Caching;
using LearningPlatform.BuildingBlocks.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Host;

/// <summary>Explicit composition of RLMS modules, shared by the API and the schema migrator.</summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<IModule> Create() =>
    [
        new Identity.IdentityModule(),
        new Academics.AcademicsModule(),
        new Classroom.ClassroomModule(),
        new Assessment.AssessmentModule(),
        new Learning.LearningModule(),
        new Communication.CommunicationModule(),
        new Reporting.ReportingModule()
    ];

    public static void AddRlmsBackend(this IHostApplicationBuilder builder)
    {
        var modules = Create();
        builder.Services.AddSingleton(modules);
        builder.Services.AddProblemDetails();
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);
        builder.AddPersistence();
        builder.AddCaching();
        builder.AddObjectStorage();
        foreach (var module in modules)
            module.ConfigureServices(builder);
        builder.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(ModuleCatalog).Assembly;
            options.Policies.UseDurableLocalQueues();
            options.AutoBuildMessageStorageOnStartup = JasperFx.AutoCreate.None;
            foreach (var module in modules)
            {
                options.Discovery.IncludeAssembly(module.GetType().Assembly);
                module.ConfigureMessaging(options);
            }
        });
    }
}
