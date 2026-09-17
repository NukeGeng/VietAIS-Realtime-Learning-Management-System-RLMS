<template>
  <div v-if="route.name === 'login'" class="bare-route"><RouterView /></div>
  <div v-else-if="route.meta.live" class="live-route"><RouterView /></div>
  <div v-else class="app-shell" :class="{ 'is-collapsed': navigation.collapsed }">
    <aside class="sidebar">
      <div class="brand">
        <span class="brand-mark">R</span>
        <span class="brand-word sidebar-label" :class="{ 'sidebar-label-hidden': navigation.collapsed }">RLMS</span>
      </div>

      <div class="workspace-label" :class="{ 'workspace-label-hidden': navigation.collapsed }">
        <span>{{ roleLabel }}</span>
        <span class="workspace-dot"></span>
      </div>

      <nav class="sidebar-nav" aria-label="Điều hướng chính">
        <template v-for="item in items" :key="item.key">
          <RouterLink v-if="item.route" :to="item.route" class="nav-link" :class="{ active: isActive(item.route) }" :title="item.label">
            <span class="nav-icon">{{ item.icon }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': navigation.collapsed }">{{ item.label }}</span>
          </RouterLink>
          <div v-else class="nav-group">
            <button class="nav-link nav-group-toggle" :class="{ active: groupActive(item) }" :title="item.label" :aria-expanded="navigation.collapsed ? collapsedGroupKey === item.key : openGroups.has(item.key)" @click="toggleGroup(item.key, $event)">
              <span class="nav-icon">{{ item.icon }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': navigation.collapsed }">{{ item.label }}</span>
              <span class="group-chevron sidebar-label" aria-hidden="true" :class="[{ open: openGroups.has(item.key) }, { 'sidebar-label-hidden': navigation.collapsed }]" />
            </button>
            <Transition name="sidebar-children">
              <div v-if="!navigation.collapsed && openGroups.has(item.key)" class="nav-children">
                <RouterLink v-for="child in item.children" :key="child.key" :to="child.route || '/dashboard'" class="nav-child" :class="{ active: isActive(child.route || '') }">
                  <span class="child-bullet">{{ child.icon }}</span><span>{{ child.label }}</span>
                </RouterLink>
              </div>
            </Transition>
          </div>
        </template>
      </nav>

      <div class="sidebar-footer">
        <RouterLink to="/notifications" class="footer-link" :title="`Thông báo${unreadCount ? ` (${unreadCount})` : ''}`">
          <span class="nav-icon">♢</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': navigation.collapsed }">Thông báo</span>
          <span v-if="unreadCount && !navigation.collapsed" class="notification-count">{{ unreadCount }}</span>
        </RouterLink>
        <button class="profile-row" :title="auth.account?.displayName || ''" @click="profileOpen = !profileOpen">
          <span class="avatar">{{ initials }}</span>
          <span class="profile-copy" :class="{ 'profile-copy-hidden': navigation.collapsed }"><strong>{{ auth.account?.displayName }}</strong><small>{{ auth.account?.email }}</small></span>
        </button>
        <div v-if="profileOpen" class="profile-menu">
          <div class="profile-menu-name">{{ auth.account?.displayName }}</div>
          <button @click="handleLogout">Đăng xuất</button>
        </div>
        <button class="collapse-control" :title="navigation.collapsed ? 'Mở rộng menu' : 'Thu gọn menu'" :aria-expanded="!navigation.collapsed" @click="navigation.toggleCollapsed()">
          <span class="collapse-icon">{{ navigation.collapsed ? '→' : '←' }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': navigation.collapsed }">{{ navigation.collapsed ? 'Mở rộng menu' : 'Thu gọn menu' }}</span>
        </button>
      </div>
    </aside>

    <Transition name="collapsed-flyout">
      <div v-if="navigation.collapsed && collapsedGroup" class="collapsed-flyout" :style="{ top: `${collapsedFlyoutTop}px` }">
        <div class="collapsed-flyout-title">{{ collapsedGroup.label }}</div>
        <RouterLink v-for="child in collapsedGroup.children" :key="child.key" :to="child.route || '/dashboard'" class="collapsed-flyout-link" :class="{ active: isActive(child.route || '') }">
          <span class="child-bullet">{{ child.icon }}</span><span>{{ child.label }}</span>
        </RouterLink>
      </div>
    </Transition>

    <main class="main-column">
      <header class="topbar">
        <div class="topbar-title">
          <span class="eyebrow">HỆ THỐNG QUẢN LÝ HỌC TẬP TRỰC TUYẾN</span>
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
import { applicationTabRoute, useNavigationStore, type NavItem } from '../../shared/stores/navigation'
import { identityApi } from '../../shared/api/client'
import { errorMessage } from '../../shared/api/http'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const navigation = useNavigationStore()
const openGroups = ref(new Set<string>())
const collapsedGroupKey = ref<string | null>(null)
const collapsedFlyoutTop = ref(0)
const profileOpen = ref(false)
const organizationName = ref('Đang tải tổ chức')
const unreadCount = ref(0)
const authSessionKey = computed(() => auth.account ? `${auth.account.id}:${auth.account.organizationId}:${auth.role}` : '')
let seenAuthSessionKey = authSessionKey.value

const roleLabel = computed(() => auth.role === 'admin' ? 'Không gian quản trị' : auth.role === 'teacher' ? 'Không gian giảng viên' : 'Không gian sinh viên')
const items = computed(() => navigation.menuFor(auth.role, auth.permissions))
const collapsedGroup = computed(() => items.value.find(item => item.key === collapsedGroupKey.value && !item.route && item.children?.length))
const currentTitle = computed(() => navigation.activeTab?.title || String(route.meta.title || 'Tổng quan'))
const initials = computed(() => (auth.account?.displayName || 'RL').split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase())

function isActive(path: string) {
  return path === '/dashboard' ? route.path === path : route.path === path || route.path.startsWith(`${path}/`)
}

function groupActive(item: NavItem) { return item.children?.some(child => isActive(child.route || '')) ?? false }
function toggleGroup(key: string, event?: MouseEvent) {
  if (navigation.collapsed) {
    if (collapsedGroupKey.value === key) {
      collapsedGroupKey.value = null
      openGroups.value.delete(key)
      return
    }

    openGroups.value.add(key)
    collapsedGroupKey.value = key
    const trigger = event?.currentTarget as HTMLElement | null
    const triggerTop = trigger?.getBoundingClientRect().top ?? 0
    const flyoutHeight = Math.min(300, Math.max(82, (items.value.find(item => item.key === key)?.children?.length ?? 1) * 38 + 38))
    collapsedFlyoutTop.value = Math.max(12, Math.min(triggerTop, window.innerHeight - flyoutHeight - 12))
    return
  }

  openGroups.value.has(key) ? openGroups.value.delete(key) : openGroups.value.add(key)
}

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

watch(authSessionKey, (nextKey) => {
  if (seenAuthSessionKey && nextKey !== seenAuthSessionKey) {
    navigation.resetForSession()
    openGroups.value.clear()
    collapsedGroupKey.value = null
    profileOpen.value = false
    if (nextKey && route.name !== 'login') void router.replace('/dashboard')
  }
  seenAuthSessionKey = nextKey
  if (nextKey) void loadOrganization()
})

watch(() => route.fullPath, () => {
  collapsedGroupKey.value = null
  navigation.activate(applicationTabRoute(route.fullPath))
  items.value.filter(item => !item.route && groupActive(item)).forEach(item => openGroups.value.add(item.key))
})
watch(() => navigation.collapsed, collapsed => { if (!collapsed) collapsedGroupKey.value = null })
watch(() => route.name, () => { loadOrganization() })
</script>
