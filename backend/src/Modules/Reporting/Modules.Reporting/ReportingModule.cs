using LearningPlatform.Reporting.Api;
using LearningPlatform.Reporting.Infrastructure.Persistence;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace LearningPlatform.Reporting;

public sealed class ReportingModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddReportingModule();
        builder.Services.AddReportingSchema();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReportingEndpoints();
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.PublishMessage<LiveSessionEndedForReporting>().ToLocalQueue("reporting-projection");
        options.PublishMessage<AttendanceRecordChangedForReporting>().ToLocalQueue("reporting-projection");
        options.PublishMessage<EnrollmentChangedForReporting>().ToLocalQueue("reporting-projection");
        options.PublishMessage<CourseContentChangedForReporting>().ToLocalQueue("reporting-projection");
        options.PublishMessage<LearningPlatform.Learning.Contracts.GradeEntryPublishedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<LearningPlatform.Learning.Contracts.GradeEntryVisibilityChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<LearningPlatform.Learning.Contracts.GradeItemChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<LearningPlatform.Learning.Contracts.LessonProgressChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<LearningPlatform.Learning.Contracts.StudentProgressChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<AssessmentActivityChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<EssayAssignmentChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.PublishMessage<SubmissionChangedForReporting>()
            .ToLocalQueue("reporting-projection");
        options.LocalQueue("reporting-projection").Sequential();
    }
}
