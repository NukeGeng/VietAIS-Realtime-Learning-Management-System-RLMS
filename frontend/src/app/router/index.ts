import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../../shared/stores/auth'
import { applicationTabRoute, useNavigationStore } from '../../shared/stores/navigation'
import { permissions as P } from '../../shared/permissions'

const LoginPage = () => import('../../features/auth/pages/LoginPage.vue')
const DashboardPage = () => import('../../features/dashboard/pages/DashboardPage.vue')
const CoursesPage = () => import('../../features/courses/pages/CoursesPage.vue')
const CourseDetailPage = () => import('../../features/courses/pages/CourseDetailPage.vue')
const ClassesPage = () => import('../../features/classrooms/pages/ClassesPage.vue')
const ClassDetailPage = () => import('../../features/classrooms/pages/ClassDetailPage.vue')
const StudentCurriculumWorkspacePage = () => import('../../features/learning/pages/StudentCurriculumWorkspacePage.vue')
const LiveClassroomPage = () => import('../../features/classrooms/pages/LiveClassroomPage.vue')
const ActivitiesPage = () => import('../../features/assignments/pages/ActivitiesPage.vue')
const ActivityWorkspacePage = () => import('../../features/assignments/pages/ActivityWorkspacePage.vue')
const StudentAttemptWorkspacePage = () => import('../../features/assignments/pages/StudentAttemptWorkspacePage.vue')
const QuestionBankPage = () => import('../../features/assignments/pages/QuestionBankPage.vue')
const EssaysPage = () => import('../../features/essays/pages/EssaysPage.vue')
const StudentEssayWorkspacePage = () => import('../../features/essays/pages/StudentEssayWorkspacePage.vue')
const GradebookPage = () => import('../../features/gradebook/pages/GradebookPage.vue')
const GradingPage = () => import('../../features/gradebook/pages/GradingPage.vue')
const CalendarPage = () => import('../../features/calendar/pages/CalendarPage.vue')
const ProgressPage = () => import('../../features/progress/pages/ProgressPage.vue')
const ReportsPage = () => import('../../features/reports/pages/ReportsPage.vue')
const AttendancePage = () => import('../../features/reports/pages/AttendancePage.vue')
const NotificationsPage = () => import('../../features/notifications/pages/NotificationsPage.vue')
const UsersPage = () => import('../../features/admin/pages/UsersPage.vue')
const RolesPage = () => import('../../features/admin/pages/RolesPage.vue')
const OrganizationPage = () => import('../../features/admin/pages/OrganizationPage.vue')
const NotFoundPage = () => import('../../features/dashboard/pages/NotFoundPage.vue')

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: LoginPage, meta: { public: true } },
  { path: '/', redirect: '/dashboard' },
  { path: '/dashboard', name: 'dashboard', component: DashboardPage, meta: { title: 'Tổng quan' } },
  { path: '/courses', name: 'courses', component: CoursesPage, meta: { title: 'Khóa học', permissions: [P.manageCourses] } },
  { path: '/courses/:courseId', name: 'course-detail', component: CourseDetailPage, meta: { title: 'Chi tiết khóa học', permissions: [P.manageCourses] } },
  { path: '/classes', name: 'classes', component: ClassesPage, meta: { title: 'Lớp học', permissions: [P.readClasses] } },
  { path: '/classes/:classId', name: 'class-detail', component: ClassDetailPage, meta: { title: 'Chi tiết lớp học', permissions: [P.readClasses] } },
  { path: '/classes/:classId/overview', name: 'class-overview', component: ClassDetailPage, meta: { title: 'Chi tiết lớp học', permissions: [P.readClasses] } },
  { path: '/classes/:classId/content', name: 'class-content', component: ClassDetailPage, meta: { title: 'Nội dung khóa học', permissions: [P.readClasses] } },
  { path: '/classes/:classId/resources', name: 'class-resources', component: ClassDetailPage, meta: { title: 'Tài nguyên', permissions: [P.readClasses] } },
  { path: '/classes/:classId/essays', name: 'class-essays', component: ClassDetailPage, meta: { title: 'Tiểu luận', permissions: [P.readClasses] } },
  { path: '/classes/:classId/essays/:essayAssignmentId', name: 'student-essay-workspace', component: StudentEssayWorkspacePage, meta: { title: 'Bài tiểu luận', permissions: [P.submitAssessmentActivities], studentOnly: true } },
  { path: '/classes/:classId/curriculums/:curriculumId', name: 'student-curriculum-workspace', component: StudentCurriculumWorkspacePage, meta: { title: 'Không gian chương trình học', permissions: [P.readClasses] } },
  { path: '/classes/:classId/live/:sessionId', name: 'live-classroom', component: LiveClassroomPage, meta: { title: 'Lớp học trực tuyến', live: true, permissions: [P.joinLiveSessions] } },
  { path: '/activities', name: 'activities', component: ActivitiesPage, meta: { title: 'Bài tập & Bài thi', permissions: [P.submitAssessmentActivities, P.manageAssessmentActivities] } },
  { path: '/activities/:activityId', name: 'activity-workspace', component: ActivityWorkspacePage, meta: { title: 'Không gian làm bài', permissions: [P.submitAssessmentActivities, P.manageAssessmentActivities] } },
  { path: '/activities/:activityId/attempts/:attemptId', name: 'student-attempt-workspace', component: StudentAttemptWorkspacePage, meta: { title: 'Quan sát bài làm', permissions: [P.manageAssessmentActivities] } },
  { path: '/question-bank', name: 'question-bank', component: QuestionBankPage, meta: { title: 'Ngân hàng câu hỏi', permissions: [P.manageAssessmentActivities] } },
  { path: '/essays', name: 'essays', component: EssaysPage, meta: { title: 'Tiểu luận', permissions: [P.submitAssessmentActivities, P.manageEssayAssignments] } },
  { path: '/gradebook', name: 'gradebook', component: GradebookPage, meta: { title: 'Sổ điểm', permissions: [P.readAssessmentResults, P.manageGradebook] } },
  { path: '/grading', name: 'grading', component: GradingPage, meta: { title: 'Chấm bài', permissions: [P.gradeAssessmentSubmissions, P.manageGradebook] } },
  { path: '/calendar', name: 'calendar', component: CalendarPage, meta: { title: 'Lịch học', permissions: [P.readScheduling] } },
  { path: '/progress', name: 'progress', component: ProgressPage, meta: { title: 'Tiến độ', permissions: [P.readLearningProgress] } },
  { path: '/reports', name: 'reports', component: ReportsPage, meta: { title: 'Báo cáo', permissions: [P.readReports] } },
  { path: '/attendance', name: 'attendance', component: AttendancePage, meta: { title: 'Theo dõi lớp học', permissions: [P.manageAttendance] } },
  { path: '/notifications', name: 'notifications', component: NotificationsPage, meta: { title: 'Thông báo', permissions: [P.readNotifications] } },
  { path: '/admin/users', name: 'admin-users', component: UsersPage, meta: { title: 'Người dùng', permissions: [P.manageUsers] } },
  { path: '/admin/roles', name: 'admin-roles', component: RolesPage, meta: { title: 'Vai trò & Quyền', permissions: [P.manageRoles] } },
  { path: '/admin/organization', name: 'admin-organization', component: OrganizationPage, meta: { title: 'Tổ chức', permissions: [P.manageOrganizations] } },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundPage, meta: { title: 'Không tìm thấy' } }
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 })
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.init()
  if (to.meta.public) {
    return auth.isAuthenticated ? '/dashboard' : true
  }
  if (!auth.isAuthenticated) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.meta.studentOnly && auth.role !== 'student') {
    return `/classes/${String(to.params.classId)}/essays`
  }
  // The top-level essay route is the teacher management workspace. Students
  // must enter an assignment through their class context so a teacher UI can
  // never be exposed by a stale/deep link.
  if (to.name === 'essays' && auth.role === 'student') return '/classes'
  const requiredPermissions = to.meta.permissions as string[] | undefined
  if (requiredPermissions?.length && !auth.canAccess(requiredPermissions)) return '/dashboard'
  const navigation = useNavigationStore()
  navigation.activate(applicationTabRoute(to.fullPath))
  return true
})

router.afterEach((to) => {
  const auth = useAuthStore()
  const routeTitle = String(to.meta.title || (to.name !== 'login' ? to.name : 'RLMS') || 'RLMS')
  const title = to.name === 'calendar' && auth.role === 'teacher'
    ? 'Lịch giảng'
    : routeTitle
  if (to.name !== 'login' && to.name !== 'not-found') {
    const navigation = useNavigationStore()
    const tabRoute = applicationTabRoute(to.fullPath)
    const key = to.name === 'class-detail' || to.name === 'class-overview' || to.name === 'class-content' || to.name === 'class-resources' || to.name === 'class-essays'
      ? `class-workspace:${String(to.params.classId)}`
      : `${String(to.name)}:${JSON.stringify(to.params)}`
    navigation.addTab({
      key,
      title,
      route: tabRoute,
      closable: to.name !== 'dashboard'
    })
  }
  document.title = `${title} · RLMS`
})

export default router
