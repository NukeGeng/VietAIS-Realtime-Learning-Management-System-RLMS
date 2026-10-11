using LearningPlatform.Assessment.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Assessment.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddAssessmentSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<QuestionBankItem>();
            options.Schema.For<QuestionVersion>();
            options.Schema.For<AssessmentActivity>();
            options.Schema.For<Submission>();
            options.Schema.For<GradeItem>();
            options.Schema.For<GradeEntry>();
            options.Schema.For<GradeAdjustment>();
            options.Schema.For<AssessmentRubric>();
            options.Schema.For<EssayAssignment>();
            options.Schema.For<EssayGroup>();
            options.Schema.For<EssayTopic>();
            options.Schema.For<EssayTopicChangeRequest>();
            options.Schema.For<FileAsset>();
            options.Schema.For<EssaySubmission>();
            options.Schema.For<ReportValidationResult>();
            options.Schema.For<ClassOverviewView>();
            options.Schema.For<StudentAttemptDetailView>();
        });
}
