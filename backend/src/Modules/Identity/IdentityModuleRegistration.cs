using LearningPlatform.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DomainIdentityOptions = LearningPlatform.Identity.Domain.IdentityOptions;

namespace LearningPlatform.Identity;

public static class IdentityModuleRegistration
{
    public static void AddIdentityModule(this IServiceCollection services)
    {
        services.AddOptions<DomainIdentityOptions>()
            .BindConfiguration(DomainIdentityOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
    }
}
