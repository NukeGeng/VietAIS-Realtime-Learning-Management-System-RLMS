namespace LearningPlatform.Identity.Domain;

public static class PermissionCatalog
{
    public const string ManageUsers = "identity.users.manage";
    public const string ManageOrganizations = "identity.organizations.manage";
    public const string ManageRoles = "identity.roles.manage";
    public const string ManageClassroomActivities = "classroom.activity.publish";
    public const string SubmitClassroomActivities = "classroom.activity.submit";
    public const string ManageEssayAssignments = "assessment.essays.manage";
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
    public const string GradeAssessmentSubmissions = "assessment.submissions.grade";
    public const string ManageGradebook = "assessment.gradebook.manage";
    public const string ReadAssessmentResults = "assessment.results.read";
    public const string ReadLearningProgress = "learning.progress.read";
    public const string ManageLearningResources = "learning.resources.manage";
    public const string ReadLearningResources = "learning.resources.read";
    public const string ManageAnnouncements = "communication.announcements.manage";
    public const string ReadAnnouncements = "communication.announcements.read";
    public const string ReadNotifications = "communication.notifications.read";
    public const string ReadReports = "reporting.classes.read";
    public const string ReadStudentReports = "reporting.students.read";

    public static readonly string[] Administrator =
    [
        ManageUsers, ManageOrganizations, ManageRoles,
        ManageCourses, ReadCourses, ManageClasses, ReadClasses, ManageEnrollments, ReadEnrollments,
        ManageScheduling, ReadScheduling, ManageLiveSessions, JoinLiveSessions,
        ManageClassroomActivities, SubmitClassroomActivities, ManageAttendance,
        ManageAssessmentActivities, SubmitAssessmentActivities, GradeAssessmentSubmissions,
        ManageGradebook, ReadAssessmentResults, ManageEssayAssignments,
        ReadLearningProgress, ManageLearningResources, ReadLearningResources,
        ManageAnnouncements, ReadAnnouncements, ReadNotifications, ReadReports, ReadStudentReports
    ];

    public static readonly string[] Teacher =
    [
        ManageCourses, ReadCourses, ManageClasses, ReadClasses, ManageEnrollments, ReadEnrollments,
        ManageScheduling, ReadScheduling,
        ManageLiveSessions, JoinLiveSessions, ManageClassroomActivities, ManageAttendance,
        SubmitAssessmentActivities,
        ManageAssessmentActivities, GradeAssessmentSubmissions, ManageGradebook,
        ReadAssessmentResults, ManageEssayAssignments, ReadLearningProgress,
        ManageLearningResources, ReadLearningResources,
        ManageAnnouncements, ReadAnnouncements, ReadNotifications,
        ReadReports, ReadStudentReports
    ];

    public static readonly string[] Student =
    [
        ReadCourses, ReadClasses, ReadEnrollments, ReadScheduling, JoinLiveSessions,
        SubmitClassroomActivities,
        SubmitAssessmentActivities, ReadAssessmentResults, ReadLearningProgress,
        ReadLearningResources,
        ReadNotifications, ReadAnnouncements
    ];

    public static readonly IReadOnlyDictionary<string, string[]> BuiltInRoles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ADMIN"] = Administrator,
            ["TEACHER"] = Teacher,
            ["STUDENT"] = Student
        };

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(BuiltInRoles.Values.SelectMany(value => value), StringComparer.Ordinal);
}
