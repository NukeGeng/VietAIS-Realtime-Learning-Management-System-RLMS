using Microsoft.Extensions.Configuration;
using Aspire.Hosting.ApplicationModel;
using Npgsql;

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../LearningPlatform.Api/appsettings.Local.json")),
    optional: true).AddEnvironmentVariables();

// Existing mode adopts the configured Compose infrastructure without recreating its data.
// Managed mode is opt-in; stop Compose containers first. Both modes share the same persistent volumes.
var managed = string.Equals(builder.Configuration["Infrastructure:Mode"], "Managed",
    StringComparison.OrdinalIgnoreCase);
var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Run scripts/setup-local.py before starting AppHost.");
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
var storageEndpoint = builder.Configuration["ObjectStorage:Endpoint"]
    ?? throw new InvalidOperationException("ObjectStorage:Endpoint is required.");
var bucket = builder.Configuration["ObjectStorage:Bucket"] ?? "rlms-files";
IResourceBuilder<IResourceWithConnectionString> postgres;
IResourceBuilder<IResourceWithEndpoints>? redis = null;
IResourceBuilder<IResourceWithEndpoints>? storage = null;
IResourceBuilder<ContainerResource>? storageInit = null;
if (managed)
{
    var connection = new NpgsqlConnectionStringBuilder(postgresConnection);
    builder.Configuration["Parameters:postgres-user"] = connection.Username;
    builder.Configuration["Parameters:postgres-password"] = connection.Password;
    builder.Configuration["Parameters:storage-user"] = builder.Configuration["ObjectStorage:AccessKey"];
    builder.Configuration["Parameters:storage-password"] = builder.Configuration["ObjectStorage:SecretKey"];
    var user = builder.AddParameter("postgres-user");
    var password = builder.AddParameter("postgres-password", secret: true);
    var storageUser = builder.AddParameter("storage-user", secret: true);
    var storagePassword = builder.AddParameter("storage-password", secret: true);
    postgres = builder.AddPostgres("postgres", user, password, port: 55432)
        .WithImageTag("17-alpine").WithDataVolume("rlms-dev_postgres-data")
        .AddDatabase("database", connection.Database ?? "rlms");
    // Plain TCP Redis matches the existing RLMS client; no TLS configuration change is introduced.
    redis = builder.AddContainer("redis", "redis", "8-alpine")
        .WithArgs("redis-server", "--save", "", "--appendonly", "no")
        .WithEndpoint(port: 56379, targetPort: 6379, name: "tcp", isProxied: false);
    storage = builder.AddContainer("object-storage", "rlms-minio", "RELEASE.2025-04-22T22-12-26Z")
        .WithArgs("server", "/data", "--console-address", ":9001")
        .WithEnvironment("MINIO_ROOT_USER", storageUser)
        .WithEnvironment("MINIO_ROOT_PASSWORD", storagePassword)
        .WithVolume("rlms-dev_object-data", "/data")
        .WithHttpEndpoint(port: 59000, targetPort: 9000, name: "api", isProxied: false)
        .WithHttpEndpoint(port: 59001, targetPort: 9001, name: "console", isProxied: false)
        .WithHttpHealthCheck("/minio/health/live", endpointName: "api");
    storageInit = builder.AddContainer("storage-init", "rlms-mc", "RELEASE.2025-04-16T18-13-26Z")
        .WithEntrypoint("/bin/sh")
        .WithArgs("-c", "mc alias set local \"$MINIO_ENDPOINT\" \"$MINIO_ROOT_USER\" \"$MINIO_ROOT_PASSWORD\" && mc mb --ignore-existing \"local/$MINIO_BUCKET\"")
        .WithEnvironment("MINIO_ENDPOINT", storage.GetEndpoint("api"))
        .WithEnvironment("MINIO_ROOT_USER", storageUser)
        .WithEnvironment("MINIO_ROOT_PASSWORD", storagePassword)
        .WithEnvironment("MINIO_BUCKET", bucket)
        .WaitFor(storage);
}
else
{
    postgres = builder.AddConnectionString("Postgres");
    builder.AddConnectionString("Redis");
    builder.AddExternalService("object-storage", storageEndpoint);
}

var migrator = builder.AddProject<Projects.LearningPlatform_DbMigrator>("db-migrator")
    .WithEnvironment("ConnectionStrings__Postgres", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("ConnectionStrings__Redis", redisConnection)
    .WithEnvironment("ObjectStorage__Endpoint", storageEndpoint);
var api = builder.AddProject<Projects.LearningPlatform_Api>("api", launchProfileName: "https")
    .WithEnvironment("ConnectionStrings__Postgres", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("ConnectionStrings__Redis", redisConnection)
    .WithEnvironment("ObjectStorage__Endpoint", storageEndpoint)
    .WithHttpHealthCheck("/health/ready", endpointName: "https")
    .WaitForCompletion(migrator);
if (managed)
{
    foreach (var resource in new IResourceBuilder<ProjectResource>[] { migrator, api })
    {
        resource.WaitFor(postgres);
        resource.WaitFor(redis!);
        resource.WithEnvironment("ConnectionStrings__Redis", redis!.GetEndpoint("tcp").Property(EndpointProperty.HostAndPort));
        resource.WithEnvironment("ObjectStorage__Endpoint", storage!.GetEndpoint("api"));
        resource.WaitForCompletion(storageInit!);
    }
}

builder.AddJavaScriptApp("frontend", "../../../../frontend")
    .WithNpm(installCommand: "ci")
    .WithRunScript("dev", ["--", "--host", "127.0.0.1", "--port", "5173", "--strictPort"])
    .WithEnvironment("VITE_DEV_API_TARGET", api.GetEndpoint("https"))
    .WithHttpEndpoint(port: 5173, targetPort: 5173, name: "http", isProxied: false)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
