using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Communication;

public static class CommunicationModuleRegistration
{
    public static void AddCommunicationModule(this IServiceCollection services)
    {
        // Communication has no process-wide state. Its durable documents and
        // endpoints are registered by the composition root when the use case is used.
    }
}
