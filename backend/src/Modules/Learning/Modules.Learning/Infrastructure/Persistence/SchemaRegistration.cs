using LearningPlatform.Learning.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Learning.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddLearningSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<StudentProgressRecord>();
            options.Schema.For<LearningHistoryEntry>();
            options.Schema.For<StudentLessonProgressRecord>();
            options.Schema.For<LearningResource>();
        });
}
