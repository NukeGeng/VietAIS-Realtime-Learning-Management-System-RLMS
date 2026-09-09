<template>
  <div v-if="route.name === 'login'" class="bare-route"><RouterView /></div>
  <div v-else-if="route.meta.live" class="live-route"><RouterView /></div>
  <div v-else class="app-shell" :class="{ 'is-collapsed': navigation.collapsed }">
    <aside class="sidebar">
      <div class="brand">
        <span class="brand-mark">R</span>
        <span v-if="!navigation.collapsed" class="brand-word">RLMS</span>
      </div>

      <div v-if="!navigation.collapsed" class="workspace-label">
        <span>{{ roleLabel }}</span>
        <span class="workspace-dot"></span>
      </div>

      <nav class="sidebar-nav" aria-label="Điều hướng chính">
        <template v-for="item in items" :key="item.key">
          <RouterLink v-if="item.route" :to="item.route" class="nav-link" :class="{ active: isActive(item.route) }" :title="item.label">
            <span class="nav-icon">{{ item.icon }}</span><span v-if="!navigation.collapsed">{{ item.label }}</span>
          </RouterLink>
          <div v-else class="nav-group">
            <button class="nav-link nav-group-toggle" :class="{ active: groupActive(item) }" :title="item.label" @click="toggleGroup(item.key)">
              <span class="nav-icon">{{ item.icon }}</span><span v-if="!navigation.collapsed">{{ item.label }}</span>
              <span v-if="!navigation.collapsed" class="group-chevron" :class="{ open: openGroups.has(item.key) }">⌄</span>
            </button>
            <div v-if="!navigation.collapsed && openGroups.has(item.key)" class="nav-children">
              <RouterLink v-for="child in item.children" :key="child.key" :to="child.route || '/dashboard'" class="nav-child" :class="{ active: isActive(child.route || '') }">
                <span class="child-bullet">{{ child.icon }}</span>{{ child.label }}
              </RouterLink>
            </div>
          </div>
        </template>
      </nav>

      <div class="sidebar-footer">
        <RouterLink to="/notifications" class="footer-link" :title="`Thông báo${unreadCount ? ` (${unreadCount})` : ''}`">
          <span class="nav-icon">♢</span><span v-if="!navigation.collapsed">Thông báo</span>
          <span v-if="unreadCount && !navigation.collapsed" class="notification-count">{{ unreadCount }}</span>
        </RouterLink>
        <button class="profile-row" :title="auth.account?.displayName || ''" @click="profileOpen = !profileOpen">
          <span class="avatar">{{ initials }}</span>
          <span v-if="!navigation.collapsed" class="profile-copy"><strong>{{ auth.account?.displayName }}</strong><small>{{ auth.account?.email }}</small></span>
        </button>
        <div v-if="profileOpen" class="profile-menu">
          <div class="profile-menu-name">{{ auth.account?.displayName }}</div>
          <button @click="handleLogout">Đăng xuất</button>
        </div>
        <button class="collapse-control" :title="navigation.collapsed ? 'Mở rộng menu' : 'Thu gọn menu'" @click="navigation.toggleCollapsed()">
          <span>{{ navigation.collapsed ? '→' : '←' }}</span><span v-if="!navigation.collapsed">Thu gọn menu</span>
        </button>
      </div>
    </aside>

    <main class="main-column">
      <header class="topbar">
        <div class="topbar-title">
          <span class="eyebrow">REALTIME LEARNING MANAGEMENT SYSTEM</span>
          <strong>{{ currentTitle }}</strong>
        </div>
        <div class="topbar-actions">
          <span class="tenant-chip"><span class="status-dot"></span>{{ organizationName }}</span>
          <RouterLink to="/notifications" class="icon-button" aria-label="Thông báo">♢<span v-if="unreadCount" class="icon-badge">{{ unreadCount }}</span></RouterLink>
          <span class="top-avatar">{{ initials }}</span>
        </div>
      </header>
      <OpenedTabs />
      <section class="content-area"><RouterView /></section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import OpenedTabs from '../../shared/ui/OpenedTabs.vue'
import { useAuthStore } from '../../shared/stores/auth'
import { useNavigationStore, type NavItem } from '../../shared/stores/navigation'
import { identityApi } from '../../shared/api/client'
import { errorMessage } from '../../shared/api/http'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const navigation = useNavigationStore()
const openGroups = ref(new Set<string>())
const profileOpen = ref(false)
const organizationName = ref('Đang tải tổ chức')
const unreadCount = ref(0)

const roleLabel = computed(() => auth.role === 'admin' ? 'Admin Workspace' : auth.role === 'teacher' ? 'Teacher Workspace' : 'Student Workspace')
const items = computed(() => navigation.menuFor(auth.role, auth.permissions))
const currentTitle = computed(() => navigation.activeTab?.title || String(route.meta.title || 'Tổng quan'))
const initials = computed(() => (auth.account?.displayName || 'RL').split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase())

function isActive(path: string) {
  return path === '/dashboard' ? route.path === path : route.path === path || route.path.startsWith(`${path}/`)
}

function groupActive(item: NavItem) { return item.children?.some(child => isActive(child.route || '')) ?? false }
function toggleGroup(key: string) { openGroups.value.has(key) ? openGroups.value.delete(key) : openGroups.value.add(key) }

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}

async function loadOrganization() {
  if (route.name === 'login' || !auth.isAuthenticated) return
  try {
    const organization = await identityApi.organization()
    organizationName.value = organization.name
  } catch (error) {
    organizationName.value = 'Tổ chức hiện tại'
    if (errorMessage(error)) console.debug(error)
  }
  items.value.filter(item => !item.route && groupActive(item)).forEach(item => openGroups.value.add(item.key))
}

onMounted(loadOrganization)

watch(() => route.fullPath, () => {
  navigation.activate(route.fullPath)
  items.value.filter(item => !item.route && groupActive(item)).forEach(item => openGroups.value.add(item.key))
})
watch(() => route.name, () => { loadOrganization() })
</script>
