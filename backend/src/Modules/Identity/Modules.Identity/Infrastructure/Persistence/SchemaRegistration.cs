using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace LearningPlatform.Identity.Infrastructure.Persistence;

internal static class SchemaRegistration
{
    public static void AddIdentitySchema(this IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Schema.For<Organization>().UniqueIndex(x => x.NormalizedCode);
            options.Schema.For<UserAccount>().UniqueIndex(
                Marten.Schema.UniqueIndexType.DuplicatedField,
                "mt_user_account_email",
                Marten.Schema.Indexing.Unique.TenancyScope.PerTenant,
                x => x.NormalizedEmail);
            options.Schema.For<RoleDefinition>().UniqueIndex(
                Marten.Schema.UniqueIndexType.DuplicatedField,
                "mt_role_definition_name",
                Marten.Schema.Indexing.Unique.TenancyScope.PerTenant,
                x => x.NormalizedName);
            options.Schema.For<IdentityBootstrapState>();
        });
}
