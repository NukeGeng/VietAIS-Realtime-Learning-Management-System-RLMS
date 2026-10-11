using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.BuildingBlocks.Web;

public interface IModule
{
    void ConfigureServices(IHostApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
    void ConfigureMessaging(WolverineOptions options) { }
}
