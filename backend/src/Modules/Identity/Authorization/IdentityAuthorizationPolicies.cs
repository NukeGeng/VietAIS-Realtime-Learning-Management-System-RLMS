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
    public const string ManageAssessmentActivities = "assessment-activities-manage";
    public const string SubmitAssessmentActivities = "assessment-activities-submit";
    public const string GradeAssessmentSubmissions = "assessment-submissions-grade";
    public const string ManageGradebook = "assessment-gradebook-manage";
    public const string ReadAssessmentResults = "assessment-results-read";
    public const string ReadLearningProgress = "learning-progress-read";

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
        AddPermissionPolicy(options, ManageAssessmentActivities, PermissionCatalog.ManageAssessmentActivities);
        AddPermissionPolicy(options, SubmitAssessmentActivities, PermissionCatalog.SubmitAssessmentActivities);
        AddPermissionPolicy(options, GradeAssessmentSubmissions, PermissionCatalog.GradeAssessmentSubmissions);
        AddPermissionPolicy(options, ManageGradebook, PermissionCatalog.ManageGradebook);
        AddPermissionPolicy(options, ReadAssessmentResults, PermissionCatalog.ReadAssessmentResults);
        AddPermissionPolicy(options, ReadLearningProgress, PermissionCatalog.ReadLearningProgress);
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string policyName,
        string permission)
    {
        options.AddPolicy(policyName, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission)));
    }
}
