import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { Role } from '../types'

export interface OpenedTab {
  key: string
  title: string
  route: string
  closable: boolean
}

export interface NavItem {
  key: string
  label: string
  route?: string
  icon: string
  children?: NavItem[]
  permissions?: string[]
}

const menus: Record<Role, NavItem[]> = {
  student: [
    { key: 'dashboard', label: 'Trang chủ', route: '/dashboard', icon: '⌂' },
    { key: 'learning', label: 'Học tập', icon: '◈', children: [
      { key: 'classes', label: 'Khóa học / Lớp học', route: '/classes', icon: '▦' },
      { key: 'calendar', label: 'Lịch học', route: '/calendar', icon: '◷' },
      { key: 'progress', label: 'Tiến độ', route: '/progress', icon: '↗' }
    ]},
    { key: 'assessment', label: 'Đánh giá', icon: '✓', children: [
      { key: 'activities', label: 'Bài tập & Bài thi', route: '/activities', icon: '▤' },
      { key: 'essays', label: 'Tiểu luận', route: '/essays', icon: '✎' },
      { key: 'grades', label: 'Điểm số', route: '/gradebook', icon: '◒' }
    ]},
    { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢' }
  ],
  teacher: [
    { key: 'dashboard', label: 'Tổng quan', route: '/dashboard', icon: '⌂' },
    { key: 'teaching', label: 'Giảng dạy', icon: '◈', children: [
      { key: 'classes', label: 'Lớp giảng dạy', route: '/classes', icon: '▦' },
      { key: 'courses', label: 'Khóa học', route: '/courses', icon: '▱' },
      { key: 'calendar', label: 'Lịch giảng', route: '/calendar', icon: '◷' }
    ]},
    { key: 'assessment', label: 'Đánh giá', icon: '✓', children: [
      { key: 'activities', label: 'Bài tập & Bài thi', route: '/activities', icon: '▤' },
      { key: 'question-bank', label: 'Ngân hàng câu hỏi', route: '/question-bank', icon: '⌘' },
      { key: 'essays', label: 'Tiểu luận', route: '/essays', icon: '✎' },
      { key: 'grading', label: 'Chấm bài', route: '/grading', icon: '☑' },
      { key: 'gradebook', label: 'Sổ điểm', route: '/gradebook', icon: '◒' }
    ]},
    { key: 'tracking', label: 'Theo dõi', icon: '⌁', children: [
      { key: 'progress', label: 'Tiến độ lớp', route: '/progress', icon: '↗' },
      { key: 'reports', label: 'Báo cáo', route: '/reports', icon: '▥' }
    ]},
    { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢' }
  ],
  admin: [
    { key: 'dashboard', label: 'Tổng quan', route: '/dashboard', icon: '⌂' },
    { key: 'system', label: 'Hệ thống', icon: '⚙', children: [
      { key: 'organization', label: 'Tổ chức', route: '/admin/organization', icon: '◇' },
      { key: 'users', label: 'Người dùng', route: '/admin/users', icon: '♙' },
      { key: 'roles', label: 'Vai trò & Quyền', route: '/admin/roles', icon: '⌑' }
    ]},
    { key: 'academics', label: 'Học vụ', icon: '◈', children: [
      { key: 'courses', label: 'Khóa học', route: '/courses', icon: '▱' },
      { key: 'classes', label: 'Lớp học', route: '/classes', icon: '▦' },
      { key: 'calendar', label: 'Lịch', route: '/calendar', icon: '◷' }
    ]},
    { key: 'operations', label: 'Vận hành', icon: '⌁', children: [
      { key: 'reports', label: 'Báo cáo', route: '/reports', icon: '▥' },
      { key: 'notifications', label: 'Thông báo', route: '/notifications', icon: '♢' }
    ]}
  ]
}

export const useNavigationStore = defineStore('navigation', () => {
  const collapsed = ref(false)
  const openedTabs = ref<OpenedTab[]>([{ key: 'dashboard', title: 'Tổng quan', route: '/dashboard', closable: false }])
  const activeRoute = ref('/dashboard')

  const activeTab = computed(() => openedTabs.value.find(tab => tab.route === activeRoute.value) ?? openedTabs.value[0])

  function menuFor(role: Role) { return menus[role] }

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

  function activate(route: string) { activeRoute.value = route }

  function renameTab(route: string, title: string) {
    const tab = openedTabs.value.find(item => item.route === route)
    if (tab && title.trim()) tab.title = title.trim()
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

  return { collapsed, openedTabs, activeRoute, activeTab, menuFor, addTab, activate, renameTab, closeTab, closeOthers, toggleCollapsed }
})
