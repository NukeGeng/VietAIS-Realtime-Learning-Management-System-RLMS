export type Role = 'student' | 'teacher' | 'admin'

export interface Account {
  id: string
  organizationId: string
  email: string
  displayName: string
  status: string | number
  permissions: string[]
}

export interface Organization {
  id: string
  code: string
  name: string
  status: string | number
  createdAt: string
}

export interface Course {
  id: string
  code: string
  title: string
  description: string
  status: string | number
  createdBy?: string
  createdAt?: string
  updatedAt?: string
}

export interface CourseDetail {
  course: Course
  modules: CourseModule[]
  lessons: Lesson[]
}

export interface CourseModule {
  id: string
  courseId: string
  title: string
  order: number
  status: string | number
}

export interface Lesson {
  id: string
  courseId: string
  courseModuleId: string
  title: string
  order: number
  contentSummary: string
  status: string | number
}

export interface LearningClass {
  id: string
  courseId: string
  code: string
  name: string
  status: string | number
  startDate: string
  endDate: string
}

export interface ClassDetail {
  class: LearningClass
  teacherIds: string[]
}

export interface Schedule {
  id: string
  learningClassId: string
  teacherId: string
  startsAt: string
  endsAt: string
  recurrence?: string
  status: string | number
}

export interface LiveSession {
  id: string
  learningClassId: string
  classScheduleId?: string
  teacherId: string
  status: string | number
  startedAt?: string
  endedAt?: string
  mediaRoomId: string
}

export interface LiveJoinResponse {
  session: LiveSession
  media: { serverUrl: string; roomId: string; accessToken: string }
  presenceRegistered: boolean
}

export interface AttendanceRecord {
  id: string
  liveSessionId: string
  studentId: string
  firstJoinedAt?: string
  lastLeftAt?: string
  totalDuration: string
  status: string | number
  adjustedBy?: string
  adjustmentReason?: string
}

export interface AttendanceSummary {
  totalStudents: number
  presentCount: number
  lateCount: number
  absentCount: number
  excusedCount: number
  attendancePercentage: number
  records: AttendanceRecord[]
}

export interface ActivityQuestion {
  id: string
  questionVersionId: string
  order: number
  points: number
  prompt: string
  type: string | number
  options: { key: string; text: string }[]
}

export interface QuestionBankItem {
  id: string
  courseId?: string
  topic: string
  difficulty?: string
  currentVersion: number
  status: string | number
}

export interface QuestionBankVersion {
  id: string
  questionBankItemId: string
  version: number
  prompt: string
  type: string | number
  options: { key: string; text: string }[]
}

export interface QuestionBankDetail {
  item: QuestionBankItem
  versions: QuestionBankVersion[]
}

export interface AssessmentActivity {
  id: string
  learningClassId: string
  liveSessionId?: string
  title: string
  description: string
  kind: string | number
  status: string | number
  opensAt?: string
  dueAt?: string
  timeLimit?: string
  publishedAt?: string
  questions: ActivityQuestion[]
  examPolicy?: ExamPolicy
}

export interface ExamPolicy {
  attemptLimit: number
  allowRetake: boolean
  shuffleQuestions: boolean
  passingScore?: number
  resultsReleaseAt?: string
}

export interface StudentActivity {
  id: string
  studentId: string
  activityId: string
  learningClassId: string
  liveSessionId?: string
  currentQuestionId?: string
  status: string | number
  answers: Record<string, string>
  answeredCount: number
  startedAt: string
  lastActivityAt: string
  submittedAt?: string
  version: number
  attemptNumber: number
  questionOrder: string[]
  helpRequested: boolean
  helpRequestedAt?: string
  helpAcknowledgedAt?: string
}

export interface OverviewStudent {
  studentId: string
  attemptId: string
  currentQuestionId?: string
  answeredCount: number
  totalQuestions: number
  progress: number
  status?: string | number
  lastActivityAt?: string
  helpRequested: boolean
  version: number
  attemptNumber: number
}

export interface ClassOverview {
  learningClassId: string
  activityId: string
  version: number
  updatedAt?: string
  students: OverviewStudent[]
  questionProgress: { questionId: string; order: number; answeredStudentCount: number; currentStudentCount: number }[]
}

export interface StudentDetail {
  id: string
  attemptId?: string
  organizationId: string
  studentId: string
  attemptNumber: number
  activityId: string
  learningClassId: string
  liveSessionId?: string
  currentQuestionId?: string
  status: string | number
  answers: Record<string, string>
  answeredCount: number
  totalQuestions: number
  progress: number
  startedAt: string
  lastActivityAt: string
  submittedAt?: string
  version: number
  helpRequested: boolean
}

export interface Gradebook {
  learningClassId: string
  items: GradebookItem[]
  students: GradebookStudent[]
}

export interface GradebookItem {
  gradeItemId: string
  activityId: string
  essayAssignmentId?: string
  title: string
  maxScore: number
  weight: number
}

export interface GradebookStudent {
  studentId: string
  average?: number
  weightedAverage?: number
  finalScore?: number
  cells?: { gradeItemId: string; score?: number; maxScore: number; status: string | number }[]
  entries?: { gradeItemId: string; score?: number; maxScore: number; weight: number; status?: string | number; feedback?: string; version?: number }[]
}

export interface EssayAssignment {
  id: string
  learningClassId: string
  courseId?: string
  title: string
  description: string
  deadline?: string
  maxScore: number
  maxGroupSize: number
  allowResubmission: boolean
  status: string | number
}

export interface EssayGroup {
  id: string
  essayAssignmentId: string
  learningClassId: string
  name: string
  memberIds?: string[]
  members?: { studentId: string; displayName?: string; joinedAt?: string }[]
  topicId?: string
  status: string | number
}

export interface EssayTopic {
  id: string
  essayAssignmentId: string
  essayGroupId: string
  proposedTitle: string
  canonicalTitle?: string
  status: string | number
}

export interface EssaySubmission {
  id: string
  essayAssignmentId: string
  essayGroupId: string
  version: number
  fileName?: string
  contentType?: string
  size?: number
  submittedAt?: string
  uploadedAt?: string
  status: string | number
  validationStatus?: string | number
  validationMessage?: string
  errors?: string[]
  gradeEntryId?: string
  score?: number
  feedback?: string
}

export interface Resource {
  id: string
  courseId: string
  learningClassId: string
  lessonId?: string
  liveSessionId?: string
  title: string
  description: string
  type: string | number
  externalUrl?: string
  fileName?: string
  contentType?: string
  size: number
  durationSeconds?: number
  status: string | number
  publishedAt?: string
  accessUrl?: string
}

export interface ClassReport {
  learningClassId: string
  courseId: string
  enrollmentCount: number
  activeEnrollmentCount: number
  completedEnrollmentCount: number
  activityCount: number
  publishedActivityCount: number
  submissionCount: number
  publishedGradeCount: number
  averageScorePercent: number
  attendanceSessionCount: number
  attendancePresentCount: number
  attendanceLateCount: number
  attendanceAbsentCount: number
  attendanceExcusedCount: number
  attendancePercentage: number
  overviewStudentCount: number
  overviewInProgressCount: number
  overviewSubmittedCount: number
  overviewHelpRequestedCount: number
  version: number
  updatedAt: string
}

export interface StudentReport {
  learningClassId: string
  studentId: string
  publishedGradeCount: number
  averageScorePercent: number
  completedActivityCount: number
  activityCount: number
  activityCompletionPercent: number
  lessonCount: number
  completedLessonCount: number
  lessonCompletionPercent: number
  attendanceSessionCount: number
  attendancePresentCount: number
  attendanceLateCount: number
  attendanceAbsentCount: number
  attendanceExcusedCount: number
  attendancePercentage: number
  version: number
  updatedAt: string
}

export interface Notification {
  id: string
  type: string
  title: string
  body: string
  createdAt: string
  readAt?: string
}

export function enumLabel(value: string | number | undefined, labels: string[] = []): string {
  if (typeof value === 'number') return labels[value] ?? String(value)
  if (!value) return '—'
  return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[-_]/g, ' ')
}

export function formatDate(value?: string): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value))
}

export function formatDateTime(value?: string): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}
