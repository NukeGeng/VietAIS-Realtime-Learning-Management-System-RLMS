using LearningPlatform.Communication.Api;
using LearningPlatform.Communication.Infrastructure.Persistence;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Domain;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Communication;

public sealed class CommunicationModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddCommunicationModule();
        builder.Services.AddCommunicationSchema();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapChatEndpoints();
        endpoints.MapAnnouncementEndpoints();
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.LocalQueue("notifications").Sequential();
        options.PublishMessage<NotificationRequested>().ToLocalQueue("notifications");
        options.PublishMessage<ClassNotificationRequested>().ToLocalQueue("notifications");
        options.PublishMessage<AnnouncementPublished>().ToLocalQueue("notifications");
    }
}
