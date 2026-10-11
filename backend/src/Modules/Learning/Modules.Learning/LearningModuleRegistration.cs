using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Learning;

public static class LearningModuleRegistration
{
    public static void AddLearningModule(this IServiceCollection services)
    {
        // Learning progress is durable state and is projected from published results.
    }
}
