using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Assessment;

public static class AssessmentModuleRegistration
{
    public static void AddAssessmentModule(this IServiceCollection services)
    {
        // Assessment state is persisted through the application's tenant-scoped Marten session.
    }
}
