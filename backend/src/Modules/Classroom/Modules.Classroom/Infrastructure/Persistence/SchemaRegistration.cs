using LearningPlatform.Classroom.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Classroom.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddClassroomSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<LiveSession>();
            options.Schema.For<AttendanceRecord>();
            options.Schema.For<RaiseHand>();
            options.Schema.For<HelpRequest>();
            options.Projections.Snapshot<StudentActivity>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);
        });
}
