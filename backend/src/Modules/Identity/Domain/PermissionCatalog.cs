namespace LearningPlatform.Identity.Domain;

public static class PermissionCatalog
{
    public const string ManageUsers = "identity.users.manage";
    public const string ManageOrganizations = "identity.organizations.manage";
    public const string ManageRoles = "identity.roles.manage";
    public const string ManageCourses = "academics.courses.manage";
    public const string ReadCourses = "academics.courses.read";
    public const string ManageClasses = "academics.classes.manage";
    public const string ReadClasses = "academics.classes.read";
    public const string ManageEnrollments = "academics.enrollments.manage";
    public const string ReadEnrollments = "academics.enrollments.read";
    public const string ManageScheduling = "academics.scheduling.manage";
    public const string ReadScheduling = "academics.scheduling.read";
    public const string ManageLiveSessions = "classroom.session.manage";
    public const string JoinLiveSessions = "classroom.session.join";
    public const string ManageAttendance = "classroom.attendance.manage";
    public const string ManageAssessmentActivities = "assessment.activities.manage";
    public const string SubmitAssessmentActivities = "assessment.activities.submit";

    public static readonly string[] Administrator =
    [
        ManageUsers, ManageOrganizations, ManageRoles,
        ManageCourses, ReadCourses, ManageClasses, ReadClasses, ManageEnrollments, ReadEnrollments,
        ManageScheduling, ReadScheduling, ManageLiveSessions, JoinLiveSessions,
        "classroom.activity.publish", "classroom.activity.submit", ManageAttendance,
        ManageAssessmentActivities, SubmitAssessmentActivities, "assessment.submissions.grade",
        "assessment.gradebook.manage", "assessment.results.read", "assessment.essays.manage",
        "learning.progress.read", "learning.resources.manage", "communication.announcements.manage",
        "communication.notifications.read", "reporting.classes.read", "reporting.students.read"
    ];

    public static readonly string[] Teacher =
    [
        ManageCourses, ReadCourses, ManageClasses, ReadClasses, ManageEnrollments, ReadEnrollments,
        ManageScheduling, ReadScheduling,
        ManageLiveSessions, JoinLiveSessions, "classroom.activity.publish", ManageAttendance,
        SubmitAssessmentActivities,
        ManageAssessmentActivities, "assessment.submissions.grade", "assessment.gradebook.manage",
        "assessment.results.read", "assessment.essays.manage", "learning.resources.manage",
        "communication.announcements.manage", "communication.notifications.read",
        "reporting.classes.read", "reporting.students.read"
    ];

    public static readonly string[] Student =
    [
        ReadClasses, ReadEnrollments, JoinLiveSessions, "classroom.activity.submit",
        SubmitAssessmentActivities, "assessment.results.read", "learning.progress.read",
        "communication.notifications.read"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> BuiltInRoles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ADMIN"] = Administrator,
            ["TEACHER"] = Teacher,
            ["STUDENT"] = Student
        };
}
