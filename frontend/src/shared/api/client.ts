import { request } from './http'
import type {
  Account, AssessmentActivity, AttendanceSummary, ClassDetail, ClassOverview, ClassReport, Course, CourseDetail,
  EssayAssignment, EssayGroup, EssaySubmission, EssayTopic, Gradebook, LearningClass, LiveJoinResponse,
  LiveSession, Notification, Organization, Resource, Schedule, StudentActivity, StudentDetail,
  StudentReport
} from '../types'

const list = <T>(path: string, params?: Record<string, unknown>) => request<T[]>({ method: 'get', url: path, params })
const activityKinds: Record<string, number> = { Assignment: 0, Exercise: 1, Quiz: 2, Exam: 3 }
const questionTypes: Record<string, number> = { SingleChoice: 0, MultipleChoice: 1, TrueFalse: 2, ShortText: 3, LongText: 4 }
const resourceTypes: Record<string, number> = { Document: 0, Slide: 1, Video: 2, Attachment: 3, Recording: 4 }
const classStatuses: Record<string, number> = { Preparing: 0, Active: 1, Paused: 2, Completed: 3, Archived: 4 }

export const identityApi = {
  login: (body: { organizationCode: string; email: string; password: string }) =>
    request<Account>({ method: 'post', url: '/api/identity/login', data: body }),
  logout: () => request<void>({ method: 'post', url: '/api/identity/logout' }),
  me: () => request<Account>({ method: 'get', url: '/api/identity/me' }),
  organization: () => request<Organization>({ method: 'get', url: '/api/identity/organization' }),
  updateOrganization: (name: string) => request<Organization>({ method: 'put', url: '/api/identity/organization', data: { name } }),
  users: (params?: Record<string, unknown>) => list<Account>('/api/identity/users', params),
  createUser: (body: { email: string; displayName: string; password: string; roles: string[] }) => request<Account>({ method: 'post', url: '/api/identity/users', data: body }),
  enableUser: (id: string) => request<void>({ method: 'post', url: `/api/identity/users/${id}/enable` }),
  disableUser: (id: string) => request<void>({ method: 'post', url: `/api/identity/users/${id}/disable` }),
  roles: () => list<{ id: string; name: string; permissions: string[] }>('/api/identity/roles'),
  createRole: (body: { name: string; permissions: string[] }) => request<{ id: string; name: string; permissions: string[] }>({ method: 'post', url: '/api/identity/roles', data: body }),
  deleteRole: (id: string) => request<void>({ method: 'delete', url: `/api/identity/roles/${id}` })
}

export const academicsApi = {
  courses: (params?: Record<string, unknown>) => list<Course>('/api/academics/courses', params),
  course: (id: string) => request<CourseDetail>({ method: 'get', url: `/api/academics/courses/${id}` }),
  createCourse: (body: { code: string; title: string; description?: string }) =>
    request<Course>({ method: 'post', url: '/api/academics/courses', data: body }),
  updateCourse: (id: string, body: { code: string; title: string; description?: string }) =>
    request<Course>({ method: 'put', url: `/api/academics/courses/${id}`, data: body }),
  publishCourse: (id: string) => request<Course>({ method: 'post', url: `/api/academics/courses/${id}/publish` }),
  archiveCourse: (id: string) => request<Course>({ method: 'post', url: `/api/academics/courses/${id}/archive` }),
  createModule: (courseId: string, body: { title: string; order: number }) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules`, data: body }),
  updateModule: (courseId: string, moduleId: string, body: { title: string; order: number }) => request<unknown>({ method: 'put', url: `/api/academics/courses/${courseId}/modules/${moduleId}`, data: body }),
  archiveModule: (courseId: string, moduleId: string) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules/${moduleId}/archive` }),
  createLesson: (courseId: string, moduleId: string, body: { title: string; order: number; contentSummary?: string }) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules/${moduleId}/lessons`, data: body }),
  updateLesson: (courseId: string, moduleId: string, lessonId: string, body: { title: string; order: number; contentSummary?: string }) => request<unknown>({ method: 'put', url: `/api/academics/courses/${courseId}/modules/${moduleId}/lessons/${lessonId}`, data: body }),
  archiveLesson: (courseId: string, moduleId: string, lessonId: string) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules/${moduleId}/lessons/${lessonId}/archive` }),
  publishModule: (courseId: string, moduleId: string) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules/${moduleId}/publish` }),
  publishLesson: (courseId: string, moduleId: string, lessonId: string) => request<unknown>({ method: 'post', url: `/api/academics/courses/${courseId}/modules/${moduleId}/lessons/${lessonId}/publish` }),
  classes: () => list<LearningClass>('/api/academics/classes'),
  classDetail: (id: string) => request<ClassDetail>({ method: 'get', url: `/api/academics/classes/${id}` }),
  createClass: (body: { courseId: string; code: string; name: string; startDate: string; endDate: string }) =>
    request<LearningClass>({ method: 'post', url: '/api/academics/classes', data: body }),
  assignTeacher: (classId: string, teacherId: string) => request<unknown>({ method: 'post', url: `/api/academics/classes/${classId}/teachers`, data: { teacherId } }),
  removeTeacher: (classId: string, teacherId: string) => request<void>({ method: 'delete', url: `/api/academics/classes/${classId}/teachers/${teacherId}` }),
  enrollments: (classId: string) => list<{ id: string; studentId: string; status: string | number; enrolledAt: string }>(`/api/academics/classes/${classId}/enrollments`),
  enrollStudent: (classId: string, studentId: string) => request<unknown>({ method: 'post', url: `/api/academics/classes/${classId}/enrollments`, data: { studentId } }),
  dropStudent: (classId: string, studentId: string) => request<void>({ method: 'delete', url: `/api/academics/classes/${classId}/enrollments/${studentId}` }),
  schedules: (classId: string) => list<Schedule>(`/api/academics/classes/${classId}/schedules`),
  mySchedules: () => list<Schedule>('/api/academics/schedules/me'),
  createSchedule: (classId: string, body: { teacherId: string; startsAt: string; endsAt: string; recurrence?: string }) => request<Schedule>({ method: 'post', url: `/api/academics/classes/${classId}/schedules`, data: body }),
  reschedule: (classId: string, scheduleId: string, body: { startsAt: string; endsAt: string; recurrence?: string }) => request<Schedule>({ method: 'post', url: `/api/academics/classes/${classId}/schedules/${scheduleId}/reschedule`, data: body }),
  cancelSchedule: (classId: string, scheduleId: string) => request<void>({ method: 'post', url: `/api/academics/classes/${classId}/schedules/${scheduleId}/cancel` }),
  changeClassStatus: (classId: string, status: string | number) => request<LearningClass>({ method: 'post', url: `/api/academics/classes/${classId}/status`, data: { status: typeof status === 'string' ? classStatuses[status] ?? status : status } })
}

export const classroomApi = {
  session: (id: string) => request<LiveSession>({ method: 'get', url: `/api/classroom/sessions/${id}` }),
  createSession: (classId: string, body: { classScheduleId?: string; teacherId?: string }) =>
    request<LiveSession>({ method: 'post', url: `/api/classroom/classes/${classId}/sessions`, data: body }),
  startSession: (id: string) => request<LiveSession>({ method: 'post', url: `/api/classroom/sessions/${id}/start` }),
  endSession: (id: string) => request<LiveSession>({ method: 'post', url: `/api/classroom/sessions/${id}/end` }),
  joinSession: (id: string) => request<LiveJoinResponse>({ method: 'post', url: `/api/classroom/sessions/${id}/join` }),
  leaveSession: (id: string) => request<void>({ method: 'post', url: `/api/classroom/sessions/${id}/leave` }),
  attendance: (id: string) => request<AttendanceSummary>({ method: 'get', url: `/api/classroom/sessions/${id}/attendance` }),
  adjustAttendance: (sessionId: string, studentId: string, body: { status?: string; reason?: string }) => request<unknown>({ method: 'post', url: `/api/classroom/sessions/${sessionId}/attendance/${studentId}`, data: body }),
  messages: (id: string) => list<{ id: string; senderId: string; body: string; sentAt: string }>(`/api/classroom/sessions/${id}/messages`),
  sendMessage: (id: string, body: string) => request<unknown>({ method: 'post', url: `/api/classroom/sessions/${id}/messages`, data: { body } }),
  raiseHand: (id: string) => request<unknown>({ method: 'post', url: `/api/classroom/sessions/${id}/raise-hand` }),
  lowerHand: (id: string) => request<void>({ method: 'delete', url: `/api/classroom/sessions/${id}/raise-hand` }),
  raiseHands: (id: string) => list<{ id: string; liveSessionId: string; studentId: string; status: string | number; raisedAt: string; loweredAt?: string; clearedBy?: string }>(`/api/classroom/sessions/${id}/raise-hands`),
  clearHand: (sessionId: string, studentId: string) => request<void>({ method: 'post', url: `/api/classroom/sessions/${sessionId}/raise-hands/${studentId}/clear` })
}

export const assessmentApi = {
  activities: (classId: string) => list<AssessmentActivity>(`/api/assessment/classes/${classId}/activities`),
  activity: (id: string) => request<AssessmentActivity>({ method: 'get', url: `/api/assessment/activities/${id}` }),
  createActivity: (classId: string, body: Record<string, unknown>) =>
    request<AssessmentActivity>({ method: 'post', url: `/api/assessment/classes/${classId}/activities`, data: { ...body, kind: typeof body.kind === 'string' ? activityKinds[body.kind] ?? body.kind : body.kind } }),
  publishActivity: (id: string) => request<AssessmentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/publish` }),
  publishInSession: (sessionId: string, activityId: string) => request<AssessmentActivity>({ method: 'post', url: `/api/classroom/sessions/${sessionId}/activities/${activityId}/publish` }),
  pauseActivity: (id: string) => request<AssessmentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/pause` }),
  resumeActivity: (id: string) => request<AssessmentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/resume` }),
  closeActivity: (id: string) => request<AssessmentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/close` }),
  attempt: (id: string) => request<StudentActivity>({ method: 'get', url: `/api/assessment/activities/${id}/attempt` }),
  startAttempt: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/start` }),
  openQuestion: (activityId: string, questionId: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${activityId}/attempt/questions/${questionId}/open` }),
  saveAnswer: (activityId: string, questionId: string, value: string) => request<StudentActivity>({ method: 'put', url: `/api/assessment/activities/${activityId}/attempt/answers/${questionId}`, data: { value } }),
  submitAttempt: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/submit` }),
  markIdle: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/idle` }),
  resumeAttempt: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/resume` }),
  requestHelp: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/help` }),
  withdrawHelp: (id: string) => request<StudentActivity>({ method: 'post', url: `/api/assessment/activities/${id}/attempt/help/withdraw` }),
  overview: (classId: string, activityId: string) => request<ClassOverview>({ method: 'get', url: `/api/assessment/classes/${classId}/activities/${activityId}/overview` }),
  detail: (attemptId: string) => request<StudentDetail>({ method: 'get', url: `/api/assessment/attempts/${attemptId}/detail` }),
  questionBank: () => list<import('../types').QuestionBankItem>('/api/assessment/question-bank'),
  questionBankDetail: (id: string) => request<import('../types').QuestionBankDetail>({ method: 'get', url: `/api/assessment/question-bank/${id}` }),
  createQuestionBankItem: (body: { courseId?: string; topic: string; difficulty?: string; prompt: string; type: string; options?: { key: string; text: string }[]; correctAnswers?: string[] }) => request<import('../types').QuestionBankDetail>({ method: 'post', url: '/api/assessment/question-bank', data: { ...body, type: questionTypes[body.type] ?? body.type } }),
  createQuestionVersion: (itemId: string, body: { prompt: string; type: string; options?: { key: string; text: string }[]; correctAnswers?: string[] }) => request<import('../types').QuestionBankVersion>({ method: 'post', url: `/api/assessment/question-bank/${itemId}/versions`, data: { ...body, type: questionTypes[body.type] ?? body.type } }),
  addQuestion: (activityId: string, body: { questionVersionId: string; order: number; points: number }) => request<AssessmentActivity>({ method: 'post', url: `/api/assessment/activities/${activityId}/questions`, data: body }),
  gradebook: (classId: string, mine = false) => request<Gradebook>({ method: 'get', url: `/api/assessment/classes/${classId}/gradebook${mine ? '/me' : ''}` }),
  result: (activityId: string) => request<unknown>({ method: 'get', url: `/api/assessment/activities/${activityId}/result` })
}

export const gradingApi = {
  getGradeEntry: (submissionId: string) => request<unknown>({ method: 'get', url: `/api/assessment/submissions/${submissionId}/grade` }),
  gradeSubmission: (submissionId: string, body: { questions?: { questionId: string; score: number; feedback?: string; rubricCriterionId?: string }[]; feedback?: string; rubricId?: string }) => request<unknown>({ method: 'post', url: `/api/assessment/submissions/${submissionId}/grade`, data: body }),
  publishGrade: (gradeEntryId: string) => request<unknown>({ method: 'post', url: `/api/assessment/grade-entries/${gradeEntryId}/publish` }),
  adjustGrade: (gradeEntryId: string, body: { score: number; feedback?: string; reason?: string }) => request<unknown>({ method: 'post', url: `/api/assessment/grade-entries/${gradeEntryId}/adjust`, data: body }),
  adjustments: (gradeEntryId: string) => list<unknown>(`/api/assessment/grade-entries/${gradeEntryId}/adjustments`),
  updateGradeItem: (gradeItemId: string, weight: number) => request<unknown>({ method: 'put', url: `/api/assessment/grade-items/${gradeItemId}`, data: { weight } })
}

export const assessmentManagementApi = {
  helpRequests: (classId: string) => list<unknown>(`/api/assessment/classes/${classId}/help-requests`),
  acknowledgeHelp: (attemptId: string) => request<unknown>({ method: 'post', url: `/api/assessment/attempts/${attemptId}/help/acknowledge` }),
  resolveHelp: (attemptId: string) => request<unknown>({ method: 'post', url: `/api/assessment/attempts/${attemptId}/help/resolve` }),
  rubric: (activityId: string) => request<unknown>({ method: 'get', url: `/api/assessment/activities/${activityId}/rubric` }),
  saveRubric: (activityId: string, criteria: unknown[]) => request<unknown>({ method: 'post', url: `/api/assessment/activities/${activityId}/rubric`, data: { criteria } })
}

export const essayApi = {
  assignments: (classId: string) => list<EssayAssignment>(`/api/assessment/classes/${classId}/essay-assignments`),
  createAssignment: (classId: string, body: { title: string; description?: string; deadline?: string; maxScore: number; maxGroupSize: number; allowResubmission: boolean; validationPolicy?: unknown }) =>
    request<EssayAssignment>({ method: 'post', url: `/api/assessment/classes/${classId}/essay-assignments`, data: body }),
  assignment: (id: string) => request<EssayAssignment>({ method: 'get', url: `/api/assessment/essay-assignments/${id}` }),
  publishAssignment: (id: string) => request<EssayAssignment>({ method: 'post', url: `/api/assessment/essay-assignments/${id}/publish` }),
  closeAssignment: (id: string) => request<EssayAssignment>({ method: 'post', url: `/api/assessment/essay-assignments/${id}/close` }),
  groups: (assignmentId: string) => list<EssayGroup>(`/api/assessment/essay-assignments/${assignmentId}/groups`),
  createGroup: (assignmentId: string, name?: string) => request<EssayGroup>({ method: 'post', url: `/api/assessment/essay-assignments/${assignmentId}/groups`, data: { name } }),
  addMember: (groupId: string, studentId: string) => request<EssayGroup>({ method: 'post', url: `/api/assessment/essay-groups/${groupId}/members`, data: { studentId } }),
  topic: (groupId: string) => request<EssayTopic>({ method: 'get', url: `/api/assessment/essay-groups/${groupId}/topic` }),
  requestTopicChange: (groupId: string, proposedTitle: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-groups/${groupId}/topic/change-request`, data: { proposedTitle } }),
  approveTopic: (topicId: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-topics/${topicId}/approve` }),
  requestTopicChanges: (topicId: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-topics/${topicId}/request-changes` }),
  rejectTopic: (topicId: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-topics/${topicId}/reject` }),
  lockTopic: (topicId: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-topics/${topicId}/lock` }),
  submissions: (groupId: string) => list<EssaySubmission>(`/api/assessment/essay-groups/${groupId}/submissions`),
  submissionFile: (submissionId: string) => request<{ submissionId: string; fileName: string; contentType: string; size: number; accessUrl: string }>({ method: 'get', url: `/api/assessment/essay-submissions/${submissionId}/file` }),
  gradeSubmission: (submissionId: string, score: number, feedback?: string) => request<unknown>({ method: 'post', url: `/api/assessment/essay-submissions/${submissionId}/grade`, data: { score, feedback } }),
  publishGrade: (gradeEntryId: string) => request<unknown>({ method: 'post', url: `/api/assessment/essay-grade-entries/${gradeEntryId}/publish` }),
  result: (assignmentId: string, groupId: string) => request<unknown>({ method: 'get', url: `/api/assessment/essay-assignments/${assignmentId}/groups/${groupId}/result` }),
  registerTopic: (groupId: string, proposedTitle: string) => request<EssayTopic>({ method: 'post', url: `/api/assessment/essay-groups/${groupId}/topic`, data: { proposedTitle } }),
  uploadSubmission: (assignmentId: string, groupId: string, file: File) => {
    const body = new FormData()
    body.append('file', file)
    return request<EssaySubmission>({ method: 'post', url: `/api/assessment/essay-assignments/${assignmentId}/groups/${groupId}/submissions`, data: body })
  }
}

export const learningApi = {
  classProgress: (classId: string) => request<{ learningClassId: string; entries: unknown[] }>({ method: 'get', url: `/api/learning/classes/${classId}/progress` }),
  myProgress: (classId: string) => request<{ learningClassId: string; entries: unknown[] }>({ method: 'get', url: `/api/learning/classes/${classId}/progress/me` }),
  courseProgress: (classId: string, mine = true) => request<unknown>({ method: 'get', url: `/api/learning/classes/${classId}/course-progress${mine ? '/me' : ''}` }),
  resources: (classId: string) => list<Resource>(`/api/learning/classes/${classId}/resources`),
  createResource: (classId: string, body: { lessonId?: string; liveSessionId?: string; title: string; description?: string; type: string; externalUrl?: string; durationSeconds?: number }) => request<Resource>({ method: 'post', url: `/api/learning/classes/${classId}/resources`, data: { ...body, type: resourceTypes[body.type] ?? body.type } }),
  uploadResource: (classId: string, file: File, metadata: { title: string; description?: string; type: string; lessonId?: string; liveSessionId?: string }) => { const body = new FormData(); body.append('file', file); body.append('title', metadata.title); if (metadata.description) body.append('description', metadata.description); body.append('type', String(resourceTypes[metadata.type] ?? metadata.type)); if (metadata.lessonId) body.append('lessonId', metadata.lessonId); if (metadata.liveSessionId) body.append('liveSessionId', metadata.liveSessionId); return request<Resource>({ method: 'post', url: `/api/learning/classes/${classId}/resources/upload`, data: body }) },
  publishResource: (id: string) => request<Resource>({ method: 'post', url: `/api/learning/resources/${id}/publish` }),
  archiveResource: (id: string) => request<Resource>({ method: 'post', url: `/api/learning/resources/${id}/archive` }),
  updateLessonProgress: (classId: string, lessonId: string, status: number) => request<unknown>({ method: 'post', url: `/api/learning/classes/${classId}/lessons/${lessonId}/progress`, data: { status } })
}

export const reportingApi = {
  classSummary: (classId: string) => request<ClassReport>({ method: 'get', url: `/api/reporting/classes/${classId}/summary` }),
  courseSummary: (courseId: string) => request<unknown>({ method: 'get', url: `/api/reporting/courses/${courseId}/summary` }),
  myStudentReport: (classId: string) => request<StudentReport>({ method: 'get', url: `/api/reporting/classes/${classId}/students/me` }),
  studentReport: (classId: string, studentId: string) => request<StudentReport>({ method: 'get', url: `/api/reporting/classes/${classId}/students/${studentId}` }),
  activitySummary: (activityId: string) => request<unknown>({ method: 'get', url: `/api/reporting/activities/${activityId}/summary` }),
  essaySummary: (assignmentId: string) => request<unknown>({ method: 'get', url: `/api/reporting/essay-assignments/${assignmentId}/summary` })
}

export const communicationApi = {
  notifications: () => list<Notification>('/api/communication/notifications'),
  markRead: (id: string) => request<void>({ method: 'post', url: `/api/communication/notifications/${id}/read` }),
  announcements: (classId: string) => list<unknown>(`/api/communication/classes/${classId}/announcements`),
  createAnnouncement: (classId: string, body: { title: string; content: string }) => request<unknown>({ method: 'post', url: `/api/communication/classes/${classId}/announcements`, data: body }),
  publishAnnouncement: (id: string) => request<unknown>({ method: 'post', url: `/api/communication/announcements/${id}/publish` }),
  archiveAnnouncement: (id: string) => request<unknown>({ method: 'post', url: `/api/communication/announcements/${id}/archive` })
}
