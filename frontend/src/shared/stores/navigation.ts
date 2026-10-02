import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { Role } from '../types'
import { permissions as P, type Permission } from '../permissions'

export interface OpenedTab {
  key: string
  title: string
  headerTitle?: string
  route: string
  closable: boolean
}

export interface NavItem {
  key: string
  label: string
  route?: string
  icon: string
  children?: NavItem[]
  permissions?: Permission[]
}

/** Class tabs are one application workspace even when a class-level sub-route changes. */
export function applicationTabRoute(path: string) {
  const observer = path.match(/^\/activities\/([^/?]+)\/attempts\/[^/?]+(?:\?.*)?$/)
  if (observer) return `/activities/${observer[1]}`
  const match = path.match(/^\/classes\/([^/]+)(?:\/(?:overview|content|resources|essays))?$/)
  return match ? `/classes/${match[1]}` : path
}

const menus: Record<Role, NavItem[]> = {
  student: [
    { key: 'dashboard', label: 'Trang chủ', route: '/dashboard', icon: '⌂' },
    { key: 'learning', label: 'Học tập', icon: '◈', children: [
      { key: 'classes', label: 'Khóa học / Lớp học', route: '/classes', icon: '▦', permissions: [P.readClasses] },
      { key: 'calendar', label: 'Lịch học', route: '/calendar', icon: '◷', permissions: [P.readScheduling] },
      { key: 'progress', label: 'Tiến độ', route: '/progress', icon: '↗', permissions: [P.readLearningProgress] }
    ]},
    { key: 'assessment', label: 'Đánh giá', icon: '✓', children: [
      { key: 'activities', label: 'Bài tập & Bài thi', route: '/activities', icon: '▤', permissions: [P.submitAssessmentActivities, P.manageAssessmentActivities] },
      { key: 'essays', label: 'Tiểu luận', route: '/essays', icon: '✎', permissions: [P.submitAssessmentActivities, P.manageEssayAssignments] },
      { key: 'grades', label: 'Điểm số', route: '/gradebook', icon: '◒', permissions: [P.readAssessmentResults, P.manageGradebook] }
    ]},
    { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢', permissions: [P.readNotifications] }
  ],
  teacher: [
    { key: 'dashboard', label: 'Tổng quan', route: '/dashboard', icon: '⌂' },
    { key: 'teaching', label: 'Giảng dạy', icon: '◈', children: [
      { key: 'classes', label: 'Lớp giảng dạy', route: '/classes', icon: '▦', permissions: [P.readClasses] },
      { key: 'courses', label: 'Khóa học', route: '/courses', icon: '▱', permissions: [P.manageCourses] },
      { key: 'calendar', label: 'Lịch giảng', route: '/calendar', icon: '◷', permissions: [P.readScheduling] }
    ]},
    { key: 'assessment', label: 'Đánh giá', icon: '✓', children: [
      { key: 'activities', label: 'Bài tập & Bài thi', route: '/activities', icon: '▤', permissions: [P.submitAssessmentActivities, P.manageAssessmentActivities] },
      { key: 'question-bank', label: 'Ngân hàng câu hỏi', route: '/question-bank', icon: '⌘', permissions: [P.manageAssessmentActivities] },
      { key: 'essays', label: 'Tiểu luận', route: '/essays', icon: '✎', permissions: [P.submitAssessmentActivities, P.manageEssayAssignments] },
      { key: 'grading', label: 'Chấm bài', route: '/grading', icon: '☑', permissions: [P.gradeAssessmentSubmissions, P.manageGradebook] },
      { key: 'gradebook', label: 'Sổ điểm', route: '/gradebook', icon: '◒', permissions: [P.readAssessmentResults, P.manageGradebook] }
    ]},
    { key: 'tracking', label: 'Theo dõi lớp học', icon: '⌁', children: [
      { key: 'progress', label: 'Tiến độ lớp', route: '/progress', icon: '↗', permissions: [P.readLearningProgress] },
      { key: 'reports', label: 'Báo cáo', route: '/reports', icon: '▥', permissions: [P.readReports] },
      { key: 'attendance', label: 'Điểm danh', route: '/attendance', icon: '◷', permissions: [P.manageAttendance] }
    ]},
    { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢', permissions: [P.readNotifications] }
  ],
  admin: [
    { key: 'dashboard', label: 'Tổng quan', route: '/dashboard', icon: '⌂' },
    { key: 'system', label: 'Hệ thống', icon: '⚙', children: [
      { key: 'organization', label: 'Tổ chức', route: '/admin/organization', icon: '◇', permissions: [P.manageOrganizations] },
      { key: 'users', label: 'Người dùng', route: '/admin/users', icon: '♙', permissions: [P.manageUsers] },
      { key: 'roles', label: 'Vai trò & Quyền', route: '/admin/roles', icon: '⌑', permissions: [P.manageRoles] }
    ]},
    { key: 'academics', label: 'Học vụ', icon: '◈', children: [
      { key: 'courses', label: 'Khóa học', route: '/courses', icon: '▱', permissions: [P.manageCourses] },
      { key: 'classes', label: 'Lớp học', route: '/classes', icon: '▦', permissions: [P.readClasses] },
      { key: 'calendar', label: 'Lịch', route: '/calendar', icon: '◷', permissions: [P.readScheduling] }
    ]},
    { key: 'assessment', label: 'Đánh giá', icon: '✓', children: [
      { key: 'activities', label: 'Bài tập & Bài thi', route: '/activities', icon: '▤', permissions: [P.submitAssessmentActivities, P.manageAssessmentActivities] },
      { key: 'question-bank', label: 'Ngân hàng câu hỏi', route: '/question-bank', icon: '⌘', permissions: [P.manageAssessmentActivities] },
      { key: 'essays', label: 'Tiểu luận', route: '/essays', icon: '✎', permissions: [P.submitAssessmentActivities, P.manageEssayAssignments] },
      { key: 'grading', label: 'Chấm bài', route: '/grading', icon: '☑', permissions: [P.gradeAssessmentSubmissions, P.manageGradebook] },
      { key: 'gradebook', label: 'Sổ điểm', route: '/gradebook', icon: '◒', permissions: [P.readAssessmentResults, P.manageGradebook] }
    ]},
    { key: 'tracking', label: 'Theo dõi lớp học', icon: '⌁', children: [
      { key: 'progress', label: 'Tiến độ', route: '/progress', icon: '↗', permissions: [P.readLearningProgress] },
      { key: 'reports', label: 'Báo cáo', route: '/reports', icon: '▥', permissions: [P.readReports] },
      { key: 'attendance', label: 'Điểm danh', route: '/attendance', icon: '◷', permissions: [P.manageAttendance] }
    ]},
    { key: 'operations', label: 'Vận hành', icon: '⌁', children: [
      { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢', permissions: [P.readNotifications] }
    ]}
  ]
}

export const useNavigationStore = defineStore('navigation', () => {
  const collapsed = ref(false)
  const openedTabs = ref<OpenedTab[]>([{ key: 'dashboard', title: 'Tổng quan', route: '/dashboard', closable: false }])
  const activeRoute = ref('/dashboard')

  const activeTab = computed(() => openedTabs.value.find(tab => tab.route === activeRoute.value) ?? openedTabs.value[0])

  function menuFor(role: Role, availablePermissions: ReadonlySet<string>) {
    return filterVisibleItems(menus[role], availablePermissions)
  }

  function addTab(tab: OpenedTab) {
    const existing = openedTabs.value.find(item => item.key === tab.key || item.route === tab.route)
    if (existing) {
      activeRoute.value = existing.route
      return existing
    }
    openedTabs.value.push(tab)
    activeRoute.value = tab.route
    return tab
  }

  function activate(route: string) { activeRoute.value = applicationTabRoute(route) }

  /** Clear object tabs when the authenticated identity changes. Routes remain the source of truth. */
  function resetForSession() {
    openedTabs.value = [{ key: 'dashboard', title: 'Tổng quan', route: '/dashboard', closable: false }]
    activeRoute.value = '/dashboard'
  }

  function renameTab(route: string, title: string, headerTitle?: string) {
    const tab = openedTabs.value.find(item => item.route === applicationTabRoute(route))
    if (tab && title.trim()) {
      tab.title = title.trim()
      tab.headerTitle = headerTitle?.trim() || undefined
    }
  }

  function closeTab(key: string) {
    const index = openedTabs.value.findIndex(tab => tab.key === key)
    if (index < 0 || !openedTabs.value[index].closable) return
    const wasActive = openedTabs.value[index].route === activeRoute.value
    openedTabs.value.splice(index, 1)
    if (wasActive) activeRoute.value = openedTabs.value[Math.max(0, index - 1)]?.route ?? '/dashboard'
  }

  function closeOthers(key: string) {
    const current = openedTabs.value.find(tab => tab.key === key)
    if (!current) return
    openedTabs.value = openedTabs.value.filter(tab => !tab.closable || tab.key === key)
    activeRoute.value = current.route
  }

  function toggleCollapsed() { collapsed.value = !collapsed.value }

  return { collapsed, openedTabs, activeRoute, activeTab, menuFor, addTab, activate, resetForSession, renameTab, closeTab, closeOthers, toggleCollapsed }
})

function filterVisibleItems(items: NavItem[], availablePermissions: ReadonlySet<string>): NavItem[] {
  return items.reduce<NavItem[]>((visible, item) => {
    if (item.route) {
      if (hasPermission(item, availablePermissions)) visible.push(item)
      return visible
    }

    const children = item.children ? filterVisibleItems(item.children, availablePermissions) : []
    if (children.length > 0) visible.push({ ...item, children })
    return visible
  }, [])
}

function hasPermission(item: NavItem, availablePermissions: ReadonlySet<string>) {
  return !item.permissions?.length || item.permissions.some(permission => availablePermissions.has(permission))
}
