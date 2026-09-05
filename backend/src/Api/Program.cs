using LearningPlatform.Api.Authentication;
using LearningPlatform.Api.Middleware;
using LearningPlatform.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using LearningPlatform.Identity;
using LearningPlatform.Identity.Api;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Academics;
using LearningPlatform.Academics.Api;
using LearningPlatform.Assessment;
using LearningPlatform.Assessment.Api;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Communication;
using LearningPlatform.Communication.Api;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddProblemDetails();
builder.Services.AddApplicationAuthentication();
builder.Services.AddIdentityModule();
builder.Services.AddAcademicsModule();
builder.Services.AddAssessmentModule();
builder.Services.AddClassroomModule();
builder.Services.AddCommunicationModule();
builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);
builder.AddInfrastructure();
builder.UseWolverine(options =>
{
    options.Policies.UseDurableLocalQueues();
    options.Discovery.IncludeAssembly(typeof(ClassroomModuleRegistration).Assembly);
    options.Discovery.IncludeAssembly(typeof(AssessmentModuleRegistration).Assembly);
    options.LocalQueue("attendance").Sequential();
    options.PublishMessage<LiveSessionParticipantJoined>().ToLocalQueue("attendance");
    options.PublishMessage<LiveSessionParticipantLeft>().ToLocalQueue("attendance");
    options.PublishMessage<LiveSessionEnded>().ToLocalQueue("attendance");
    options.LocalQueue("activity-projection").Sequential();
    options.PublishMessage<StudentActivityChanged>().ToLocalQueue("activity-projection");
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiAntiforgeryMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("core")
}).AllowAnonymous();
app.MapHealthChecks("/health/dependencies", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("peripheral")
});
app.MapGet("/api/identity/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
}).AllowAnonymous();
app.MapIdentityEndpoints();
app.MapAcademicEndpoints();
app.MapClassEndpoints();
app.MapAssessmentEndpoints();
app.MapHub<AssessmentRealtimeHub>("/hubs/assessment")
    .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
app.MapLiveSessionEndpoints();
app.MapAttendanceEndpoints();
app.MapRaiseHandEndpoints();
app.MapChatEndpoints();
app.Run();

public partial class Program;
