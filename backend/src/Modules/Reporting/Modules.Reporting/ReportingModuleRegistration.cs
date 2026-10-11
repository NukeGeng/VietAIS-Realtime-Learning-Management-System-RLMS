using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Reporting;

public static class ReportingModuleRegistration
{
    public static void AddReportingModule(this IServiceCollection services)
    {
        // Reports are read models. Their projections are intentionally independent
        // from answer and submission writes.
    }
}
