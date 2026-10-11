using LearningPlatform.Reporting.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Reporting.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddReportingSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<ClassReportSnapshot>();
            options.Schema.For<StudentReportSnapshot>();
        });
}
