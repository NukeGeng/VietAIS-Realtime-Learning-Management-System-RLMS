using LearningPlatform.Identity.Domain;
using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Identity.Authorization;

public static class IdentityAuthorizationPolicies
{
    public const string ManageUsers = "identity-users-manage";
    public const string ManageCourses = "academics-courses-manage";
    public const string ReadCourses = "academics-courses-read";
    public const string ManageClasses = "academics-classes-manage";
    public const string ReadClasses = "academics-classes-read";
    public const string ManageEnrollments = "academics-enrollments-manage";
    public const string ReadEnrollments = "academics-enrollments-read";
    public const string ManageScheduling = "academics-scheduling-manage";
    public const string ReadScheduling = "academics-scheduling-read";
    public const string ManageLiveSessions = "classroom-live-session-manage";
    public const string JoinLiveSessions = "classroom-live-session-join";
    public const string ManageAttendance = "classroom-attendance-manage";

    public static void AddIdentityPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ManageUsers, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(PermissionCatalog.ManageUsers)));
        AddPermissionPolicy(options, ManageCourses, PermissionCatalog.ManageCourses);
        AddPermissionPolicy(options, ReadCourses, PermissionCatalog.ReadCourses);
        AddPermissionPolicy(options, ManageClasses, PermissionCatalog.ManageClasses);
        AddPermissionPolicy(options, ReadClasses, PermissionCatalog.ReadClasses);
        AddPermissionPolicy(options, ManageEnrollments, PermissionCatalog.ManageEnrollments);
        AddPermissionPolicy(options, ReadEnrollments, PermissionCatalog.ReadEnrollments);
        AddPermissionPolicy(options, ManageScheduling, PermissionCatalog.ManageScheduling);
        AddPermissionPolicy(options, ReadScheduling, PermissionCatalog.ReadScheduling);
        AddPermissionPolicy(options, ManageLiveSessions, PermissionCatalog.ManageLiveSessions);
        AddPermissionPolicy(options, JoinLiveSessions, PermissionCatalog.JoinLiveSessions);
        AddPermissionPolicy(options, ManageAttendance, PermissionCatalog.ManageAttendance);
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string policyName,
        string permission)
    {
        options.AddPolicy(policyName, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission)));
    }
}
