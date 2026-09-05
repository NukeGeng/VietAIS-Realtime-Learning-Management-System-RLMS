namespace LearningPlatform.Identity.Domain;

public static class PermissionCatalog
{
    public const string ManageUsers = "identity.users.manage";
    public const string ManageOrganizations = "identity.organizations.manage";
    public const string ManageRoles = "identity.roles.manage";

    public static readonly string[] Administrator =
    [
        ManageUsers, ManageOrganizations, ManageRoles,
        "academics.courses.manage", "academics.classes.manage", "academics.scheduling.manage",
        "academics.classes.read", "classroom.session.manage", "classroom.session.join",
        "classroom.activity.publish", "classroom.activity.submit", "classroom.attendance.manage",
        "assessment.activities.manage", "assessment.activities.submit", "assessment.submissions.grade",
        "assessment.gradebook.manage", "assessment.results.read", "assessment.essays.manage",
        "learning.progress.read", "learning.resources.manage", "communication.announcements.manage",
        "communication.notifications.read", "reporting.classes.read", "reporting.students.read"
    ];

    public static readonly string[] Teacher =
    [
        "academics.courses.manage", "academics.classes.manage", "academics.scheduling.manage",
        "classroom.session.manage", "classroom.activity.publish", "classroom.attendance.manage",
        "assessment.activities.manage", "assessment.submissions.grade", "assessment.gradebook.manage",
        "assessment.results.read", "assessment.essays.manage", "learning.resources.manage",
        "communication.announcements.manage", "communication.notifications.read",
        "reporting.classes.read", "reporting.students.read"
    ];

    public static readonly string[] Student =
    [
        "academics.classes.read", "classroom.session.join", "classroom.activity.submit",
        "assessment.activities.submit", "assessment.results.read", "learning.progress.read",
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
