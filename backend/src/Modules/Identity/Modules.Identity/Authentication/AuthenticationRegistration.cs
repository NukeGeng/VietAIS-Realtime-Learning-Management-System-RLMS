using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LearningPlatform.Identity.Authentication;

public static class AuthenticationRegistration
{
    public static void AddApplicationAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-rlms";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.EventsType = typeof(IdentityCookieEvents);
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<LearningPlatform.Identity.Domain.IdentityOptions>>((cookie, identity) =>
            {
                cookie.ExpireTimeSpan = TimeSpan.FromHours(identity.Value.SessionHours);
            });
        services.AddSingleton<IAuthorizationHandler, OrganizationMemberHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IdentityCookieEvents>();
        services.AddAuthorization(options =>
        {
            var membership = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().AddRequirements(new OrganizationMemberRequirement()).Build();
            options.DefaultPolicy = membership;
            options.FallbackPolicy = membership;
            IdentityAuthorizationPolicies.AddIdentityPolicies(options);
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-rlms-csrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
    }
}
