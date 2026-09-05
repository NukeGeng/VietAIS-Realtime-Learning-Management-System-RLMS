using LearningPlatform.Classroom.Presence;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Classroom;

public static class ClassroomModuleRegistration
{
    public static void AddClassroomModule(this IServiceCollection services)
    {
        services.AddSingleton<LivePresenceStore>();
    }
}
