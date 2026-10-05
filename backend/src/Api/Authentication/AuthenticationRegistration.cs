using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Api.Authentication;

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
        services.AddSingleton<IAuthorizationHandler, OrganizationMemberHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization(options =>
        {
            var membership = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().AddRequirements(new OrganizationMemberRequirement()).Build();
            options.DefaultPolicy = membership;
            options.FallbackPolicy = membership;
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
