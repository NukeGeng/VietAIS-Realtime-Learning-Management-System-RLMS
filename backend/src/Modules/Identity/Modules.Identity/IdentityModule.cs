using LearningPlatform.Identity.Api;
using LearningPlatform.Identity.Infrastructure.Persistence;
using LearningPlatform.Identity.Authentication;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Identity;

public sealed class IdentityModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddIdentityModule();
        builder.Services.AddIdentitySchema();
        builder.Services.AddApplicationAuthentication();
        builder.Services.AddScoped<LearningPlatform.BuildingBlocks.Persistence.ITenantContext, LearningPlatform.BuildingBlocks.Persistence.HttpTenantContext>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapIdentityEndpoints();
    }
}
