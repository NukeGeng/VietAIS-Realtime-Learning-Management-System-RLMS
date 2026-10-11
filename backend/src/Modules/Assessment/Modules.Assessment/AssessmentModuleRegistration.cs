using LearningPlatform.Assessment.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Assessment;

public static class AssessmentModuleRegistration
{
    public static void AddAssessmentModule(this IServiceCollection services)
    {
        services.AddSingleton<IActivityRealtimeNotifier, ActivityRealtimeNotifier>();
    }
}
