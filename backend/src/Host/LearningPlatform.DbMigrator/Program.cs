using JasperFx;
using LearningPlatform.Host;
using LearningPlatform.ServiceDefaults;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine.Persistence.Durability;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables();
builder.AddServiceDefaults();
builder.AddRlmsBackend();
using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrator");
var store = host.Services.GetRequiredService<IDocumentStore>();
// Explicit, idempotent schema setup. Do not start hosted message processors or seed business data.
await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate);
await host.Services.GetRequiredService<IMessageStore>().Admin.MigrateAsync(AutoCreate.CreateOrUpdate);
logger.LogInformation("RLMS document, event and durable-message schemas are ready.");
