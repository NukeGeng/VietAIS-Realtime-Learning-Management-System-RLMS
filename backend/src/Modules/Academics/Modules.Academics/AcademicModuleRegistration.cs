using LearningPlatform.Academics.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Academics;

public static class AcademicModuleRegistration
{
    public static void AddAcademicsModule(this IServiceCollection services)
    {
        services.AddScoped<IAcademicDirectory, AcademicDirectory>();
    }
}
