using LearningPlatform.Academics.Api;
using LearningPlatform.Academics.Infrastructure.Persistence;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Academics;

public sealed class AcademicsModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAcademicsModule();
        builder.Services.AddAcademicsSchema();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAcademicEndpoints();
        endpoints.MapClassEndpoints();
    }
}
