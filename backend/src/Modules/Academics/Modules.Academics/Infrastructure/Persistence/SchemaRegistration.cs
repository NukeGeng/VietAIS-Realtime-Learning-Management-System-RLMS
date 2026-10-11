using LearningPlatform.Academics.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Academics.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddAcademicsSchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<Course>().UniqueIndex(
                Marten.Schema.UniqueIndexType.DuplicatedField,
                "mt_course_code",
                Marten.Schema.Indexing.Unique.TenancyScope.PerTenant,
                x => x.Code);
            options.Schema.For<Curriculum>();
            options.Schema.For<ClassCurriculumAssignment>();
            options.Schema.For<CourseModule>();
            options.Schema.For<Lesson>();
            options.Schema.For<LearningClass>().UniqueIndex(
                Marten.Schema.UniqueIndexType.DuplicatedField,
                "mt_learning_class_code",
                Marten.Schema.Indexing.Unique.TenancyScope.PerTenant,
                x => x.Code);
            options.Schema.For<Enrollment>();
            options.Schema.For<ClassTeacherAssignment>();
            options.Schema.For<ClassSchedule>();
        });
}
