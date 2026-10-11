using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Infrastructure.Persistence;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Assessment;

public sealed class AssessmentModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAssessmentModule();
        builder.Services.AddAssessmentSchema();
        builder.Services.AddSingleton<LearningPlatform.Assessment.Storage.IFileStorage, LearningPlatform.Assessment.Infrastructure.Storage.EssayFileStorage>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAssessmentEndpoints();
        endpoints.MapHub<LearningPlatform.Assessment.Realtime.AssessmentRealtimeHub>("/hubs/assessment")
            .RequireAuthorization(LearningPlatform.Identity.Authorization.IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.LocalQueue("activity-projection").Sequential();
        options.PublishMessage<StudentActivityChanged>().ToLocalQueue("activity-projection");
    }
}
