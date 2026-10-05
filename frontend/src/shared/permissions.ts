export const permissions = {
  manageOrganizations: 'identity.organizations.manage',
  manageUsers: 'identity.users.manage',
  manageRoles: 'identity.roles.manage',
  manageCourses: 'academics.courses.manage',
  readCourses: 'academics.courses.read',
  manageClasses: 'academics.classes.manage',
  readClasses: 'academics.classes.read',
  readScheduling: 'academics.scheduling.read',
  joinLiveSessions: 'classroom.session.join',
  manageAssessmentActivities: 'assessment.activities.manage',
  submitAssessmentActivities: 'assessment.activities.submit',
  gradeAssessmentSubmissions: 'assessment.submissions.grade',
  manageGradebook: 'assessment.gradebook.manage',
  readAssessmentResults: 'assessment.results.read',
  manageEssayAssignments: 'assessment.essays.manage',
  readLearningProgress: 'learning.progress.read',
  readNotifications: 'communication.notifications.read',
  manageAttendance: 'classroom.attendance.manage',
  readReports: 'reporting.classes.read'
} as const

export type Permission = typeof permissions[keyof typeof permissions]
