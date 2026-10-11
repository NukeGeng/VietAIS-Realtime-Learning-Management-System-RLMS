using LearningPlatform.BuildingBlocks.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JasperFx;
using Marten;
using Npgsql;
using Wolverine.Marten;

namespace LearningPlatform.BuildingBlocks.Persistence;

public static class PersistenceRegistration
{
    public static void AddPersistence(this IHostApplicationBuilder builder)
    {
        var postgres = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        builder.Services.AddMarten(options =>
        {
            options.Connection(postgres);
            options.DatabaseSchemaName = "rlms";
            options.Policies.AllDocumentsAreMultiTenanted();
            options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Conjoined;
            options.AutoCreateSchemaObjects = AutoCreate.None;
        }).UseLightweightSessions().IntegrateWithWolverine();

        builder.Services.AddHttpContextAccessor();
        // Override default HTTP session resolution: there is no unscoped fallback.
        builder.Services.AddScoped<IDocumentSession>(sp => OrganizationSession.Open(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<ITenantContext>()));
        builder.Services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentSession>());
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(postgres));
        builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres",
            tags: ["core"], timeout: TimeSpan.FromSeconds(5));
    }
}
