using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Infrastructure.Persistence;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Microsoft.Extensions.Options;
using LearningPlatform.Classroom.Infrastructure.Media;

namespace LearningPlatform.Classroom;

public sealed class ClassroomModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddClassroomModule();
        builder.Services.AddClassroomSchema();
        builder.Services.AddOptions<MediaOptions>().BindConfiguration("Media")
            .ValidateDataAnnotations()
            .Validate(o => Uri.TryCreate(o.ServerUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "ws" or "wss", "Media:ServerUrl must use ws or wss.")
            .ValidateOnStart();

    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLiveSessionEndpoints();
        endpoints.MapLiveSessionModerationEndpoints();
        endpoints.MapAttendanceEndpoints();
        endpoints.MapRaiseHandEndpoints();
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.LocalQueue("attendance").Sequential();
        options.PublishMessage<LiveSessionParticipantJoined>().ToLocalQueue("attendance");
        options.PublishMessage<LiveSessionParticipantLeft>().ToLocalQueue("attendance");
        options.PublishMessage<LiveSessionEnded>().ToLocalQueue("attendance");
    }
}
