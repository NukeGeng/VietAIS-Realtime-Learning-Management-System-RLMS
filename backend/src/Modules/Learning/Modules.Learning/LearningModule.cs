using LearningPlatform.Learning.Api;
using LearningPlatform.Learning.Infrastructure.Persistence;

using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Learning;

public sealed class LearningModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddLearningModule();
        builder.Services.AddLearningSchema();
        builder.Services.AddSingleton<LearningPlatform.Learning.Storage.ILearningResourceStorage, LearningPlatform.Learning.Infrastructure.Storage.LearningResourceStorage>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLearningEndpoints();
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.LocalQueue("learning-progress").Sequential();
        options.PublishMessage<LearningPlatform.Learning.Contracts.GradeEntryPublished>()
            .ToLocalQueue("learning-progress");
        options.PublishMessage<LearningPlatform.Learning.Contracts.GradeEntryVisibilityChanged>()
            .ToLocalQueue("learning-progress");
    }
}
