import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../../shared/stores/auth'
import { useNavigationStore } from '../../shared/stores/navigation'

const LoginPage = () => import('../../features/auth/pages/LoginPage.vue')
const DashboardPage = () => import('../../features/dashboard/pages/DashboardPage.vue')
const CoursesPage = () => import('../../features/courses/pages/CoursesPage.vue')
const CourseDetailPage = () => import('../../features/courses/pages/CourseDetailPage.vue')
const ClassesPage = () => import('../../features/classrooms/pages/ClassesPage.vue')
const ClassDetailPage = () => import('../../features/classrooms/pages/ClassDetailPage.vue')
const LiveClassroomPage = () => import('../../features/classrooms/pages/LiveClassroomPage.vue')
const ActivitiesPage = () => import('../../features/assignments/pages/ActivitiesPage.vue')
const ActivityWorkspacePage = () => import('../../features/assignments/pages/ActivityWorkspacePage.vue')
const QuestionBankPage = () => import('../../features/assignments/pages/QuestionBankPage.vue')
const EssaysPage = () => import('../../features/essays/pages/EssaysPage.vue')
const GradebookPage = () => import('../../features/gradebook/pages/GradebookPage.vue')
const GradingPage = () => import('../../features/gradebook/pages/GradingPage.vue')
const CalendarPage = () => import('../../features/calendar/pages/CalendarPage.vue')
const ProgressPage = () => import('../../features/progress/pages/ProgressPage.vue')
const ReportsPage = () => import('../../features/reports/pages/ReportsPage.vue')
const NotificationsPage = () => import('../../features/notifications/pages/NotificationsPage.vue')
const UsersPage = () => import('../../features/admin/pages/UsersPage.vue')
const RolesPage = () => import('../../features/admin/pages/RolesPage.vue')
const OrganizationPage = () => import('../../features/admin/pages/OrganizationPage.vue')
const NotFoundPage = () => import('../../features/dashboard/pages/NotFoundPage.vue')

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: LoginPage, meta: { public: true } },
  { path: '/', redirect: '/dashboard' },
  { path: '/dashboard', name: 'dashboard', component: DashboardPage, meta: { title: 'Tổng quan' } },
  { path: '/courses', name: 'courses', component: CoursesPage, meta: { title: 'Khóa học', roles: ['teacher', 'admin'] } },
  { path: '/courses/:courseId', name: 'course-detail', component: CourseDetailPage, meta: { title: 'Chi tiết khóa học' } },
  { path: '/classes', name: 'classes', component: ClassesPage, meta: { title: 'Lớp học' } },
  { path: '/classes/:classId', name: 'class-detail', component: ClassDetailPage, meta: { title: 'Chi tiết lớp học' } },
  { path: '/classes/:classId/live/:sessionId', name: 'live-classroom', component: LiveClassroomPage, meta: { title: 'Live Classroom', live: true } },
  { path: '/activities', name: 'activities', component: ActivitiesPage, meta: { title: 'Bài tập & Bài thi' } },
  { path: '/activities/:activityId', name: 'activity-workspace', component: ActivityWorkspacePage, meta: { title: 'Activity Workspace', roles: ['student', 'teacher'] } },
  { path: '/question-bank', name: 'question-bank', component: QuestionBankPage, meta: { title: 'Ngân hàng câu hỏi', roles: ['teacher', 'admin'] } },
  { path: '/essays', name: 'essays', component: EssaysPage, meta: { title: 'Tiểu luận' } },
  { path: '/gradebook', name: 'gradebook', component: GradebookPage, meta: { title: 'Sổ điểm' } },
  { path: '/grading', name: 'grading', component: GradingPage, meta: { title: 'Chấm bài', roles: ['teacher', 'admin'] } },
  { path: '/calendar', name: 'calendar', component: CalendarPage, meta: { title: 'Lịch học' } },
  { path: '/progress', name: 'progress', component: ProgressPage, meta: { title: 'Tiến độ' } },
  { path: '/reports', name: 'reports', component: ReportsPage, meta: { title: 'Báo cáo', roles: ['teacher', 'admin'] } },
  { path: '/notifications', name: 'notifications', component: NotificationsPage, meta: { title: 'Thông báo' } },
  { path: '/admin/users', name: 'admin-users', component: UsersPage, meta: { title: 'Người dùng', roles: ['admin'] } },
  { path: '/admin/roles', name: 'admin-roles', component: RolesPage, meta: { title: 'Vai trò & Quyền', roles: ['admin'] } },
  { path: '/admin/organization', name: 'admin-organization', component: OrganizationPage, meta: { title: 'Tổ chức', roles: ['admin'] } },
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
  const roles = to.meta.roles as string[] | undefined
  if (roles?.length && !roles.includes(auth.role)) return '/dashboard'
  const navigation = useNavigationStore()
  navigation.activate(to.fullPath)
  return true
})

router.afterEach((to) => {
  if (to.name !== 'login' && to.name !== 'not-found') {
    const navigation = useNavigationStore()
    const key = `${String(to.name)}:${JSON.stringify(to.params)}`
    navigation.addTab({
      key,
      title: String(to.meta.title || to.name || 'RLMS'),
      route: to.fullPath,
      closable: to.name !== 'dashboard'
    })
  }
  document.title = `${String(to.meta.title || 'RLMS')} · RLMS`
})

export default router
