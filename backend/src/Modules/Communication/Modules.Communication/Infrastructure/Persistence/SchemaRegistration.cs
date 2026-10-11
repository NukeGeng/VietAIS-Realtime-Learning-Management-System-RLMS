using LearningPlatform.Communication.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Communication.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddCommunicationSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<ChatMessage>();
            options.Schema.For<Announcement>();
            options.Schema.For<Notification>();
        });
}
