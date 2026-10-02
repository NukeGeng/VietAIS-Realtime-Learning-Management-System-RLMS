<template>
  <div v-if="route.name === 'login'" class="bare-route"><RouterView /></div>
  <div v-else-if="route.meta.live" class="live-route"><RouterView /></div>
  <div v-else class="app-shell" :class="{ 'is-collapsed': sidebarCollapsed, 'mobile-navigation-open': mobileNavigationOpen }">
    <div v-if="isMobile && mobileNavigationOpen" class="sidebar-backdrop" aria-hidden="true" @click="closeMobileNavigation()" />
    <aside id="workspace-navigation" ref="sidebarElement" class="sidebar" :inert="isMobile && !mobileNavigationOpen" :aria-hidden="isMobile && !mobileNavigationOpen ? true : undefined">
      <div class="brand">
        <span class="brand-mark">R</span>
        <span class="brand-word sidebar-label" :class="{ 'sidebar-label-hidden': sidebarCollapsed }">RLMS</span>
        <button v-if="isMobile" type="button" class="icon-button mobile-navigation-close" aria-label="Đóng menu điều hướng" @click="closeMobileNavigation()"><XMarkIcon /></button>
      </div>

      <div class="workspace-label" :class="{ 'workspace-label-hidden': sidebarCollapsed }">
        <span>{{ roleLabel }}</span>
        <span class="workspace-dot"></span>
      </div>

      <nav class="sidebar-nav" aria-label="Điều hướng chính">
        <template v-for="item in items" :key="item.key">
          <RouterLink v-if="item.route" :to="item.route" class="nav-link" :class="{ active: isActive(item.route) }" :title="item.label" :aria-label="item.label" @click="closeMobileNavigation()">
            <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': sidebarCollapsed }">{{ item.label }}</span>
          </RouterLink>
          <div v-else class="nav-group">
            <button class="nav-link nav-group-toggle" :class="{ active: groupActive(item) }" :title="item.label" :aria-label="item.label" :aria-expanded="sidebarCollapsed ? collapsedGroupKey === item.key : openGroups.has(item.key)" @click="toggleGroup(item.key, $event)">
              <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': sidebarCollapsed }">{{ item.label }}</span>
              <span class="group-chevron sidebar-label" aria-hidden="true" :class="[{ open: openGroups.has(item.key) }, { 'sidebar-label-hidden': sidebarCollapsed }]" />
            </button>
            <Transition name="sidebar-children">
              <div v-if="!sidebarCollapsed && openGroups.has(item.key)" class="nav-children">
                <RouterLink v-for="child in item.children" :key="child.key" :to="child.route || '/dashboard'" class="nav-child" :class="{ active: isActive(child.route || '') }" @click="closeMobileNavigation()">
                  <span class="child-bullet">{{ child.icon }}</span><span>{{ child.label }}</span>
                </RouterLink>
              </div>
            </Transition>
          </div>
        </template>
      </nav>

      <div class="sidebar-footer">
        <RouterLink to="/notifications" class="footer-link" :title="`Thông báo${unreadCount ? ` (${unreadCount})` : ''}`" aria-label="Thông báo" @click="closeMobileNavigation()">
          <span class="nav-icon" aria-hidden="true">♢</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': sidebarCollapsed }">Thông báo</span>
          <span v-if="unreadCount && !sidebarCollapsed" class="notification-count">{{ unreadCount }}</span>
        </RouterLink>
        <button id="account-menu-trigger" ref="profileTrigger" type="button" class="profile-row" :title="auth.account?.displayName || ''" :aria-label="`Tài khoản ${auth.account?.displayName || ''}`" aria-haspopup="menu" :aria-expanded="profileOpen" aria-controls="account-menu" @click="toggleProfile">
          <span class="avatar">{{ initials }}</span>
          <span class="profile-copy" :class="{ 'profile-copy-hidden': sidebarCollapsed }"><strong>{{ auth.account?.displayName }}</strong><small>{{ auth.account?.email }}</small></span>
        </button>
        <button v-if="!isMobile" class="collapse-control" :title="navigation.collapsed ? 'Mở rộng menu' : 'Thu gọn menu'" :aria-expanded="!navigation.collapsed" @click="navigation.toggleCollapsed()">
          <span class="collapse-icon">{{ navigation.collapsed ? '→' : '←' }}</span><span class="sidebar-label" :class="{ 'sidebar-label-hidden': sidebarCollapsed }">{{ navigation.collapsed ? 'Mở rộng menu' : 'Thu gọn menu' }}</span>
        </button>
      </div>
    </aside>

    <Transition name="collapsed-flyout">
      <div v-if="sidebarCollapsed && collapsedGroup" class="collapsed-flyout" :style="{ top: `${collapsedFlyoutTop}px` }">
        <div class="collapsed-flyout-title">{{ collapsedGroup.label }}</div>
        <RouterLink v-for="child in collapsedGroup.children" :key="child.key" :to="child.route || '/dashboard'" class="collapsed-flyout-link" :class="{ active: isActive(child.route || '') }">
          <span class="child-bullet">{{ child.icon }}</span><span>{{ child.label }}</span>
        </RouterLink>
      </div>
    </Transition>

    <div v-if="profileOpen" id="account-menu" ref="profileMenu" class="profile-menu" :style="profilePosition" role="menu" aria-labelledby="account-menu-trigger" @focusout="onProfileFocusOut">
      <div class="profile-menu-name" role="presentation">{{ auth.account?.displayName }}</div>
      <button type="button" role="menuitem" @click="handleLogout">Đăng xuất</button>
    </div>

    <main class="main-column" :inert="isMobile && mobileNavigationOpen">
      <header class="topbar">
        <button v-if="isMobile" ref="mobileNavigationTrigger" type="button" class="icon-button mobile-navigation-toggle" aria-label="Mở menu điều hướng" aria-controls="workspace-navigation" :aria-expanded="mobileNavigationOpen" @click="openMobileNavigation"><Bars3Icon /></button>
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
      <section id="workspace-page" class="content-area" role="tabpanel" :aria-label="currentTitle"><RouterView /></section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Bars3Icon, XMarkIcon } from '@heroicons/vue/24/outline'
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
const mobileViewport = window.matchMedia('(max-width: 760px)')
const isMobile = ref(mobileViewport.matches)
const mobileNavigationOpen = ref(false)
const sidebarElement = ref<HTMLElement | null>(null)
const mobileNavigationTrigger = ref<HTMLButtonElement | null>(null)
const profileTrigger = ref<HTMLButtonElement | null>(null)
const profileMenu = ref<HTMLElement | null>(null)
const profilePosition = ref({ left: '12px', top: '12px' })
const sidebarCollapsed = computed(() => !isMobile.value && navigation.collapsed)
let previousBodyOverflow: string | undefined
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
  if (sidebarCollapsed.value) {
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

async function openMobileNavigation() {
  mobileNavigationOpen.value = true
  await nextTick()
  sidebarElement.value?.querySelector<HTMLButtonElement>('button')?.focus()
}

async function closeMobileNavigation(restoreFocus = true) {
  if (!mobileNavigationOpen.value) return
  mobileNavigationOpen.value = false
  profileOpen.value = false
  await nextTick()
  if (restoreFocus) mobileNavigationTrigger.value?.focus()
}

function closeProfile(restoreFocus = false) {
  profileOpen.value = false
  if (restoreFocus) profileTrigger.value?.focus()
}

function positionProfile() {
  const trigger = profileTrigger.value?.getBoundingClientRect()
  const popup = profileMenu.value?.getBoundingClientRect()
  if (!trigger || !popup) return
  const left = sidebarCollapsed.value ? (sidebarElement.value?.getBoundingClientRect().right ?? trigger.right) + 8 : trigger.left
  profilePosition.value = {
    left: `${Math.max(12, Math.min(left, window.innerWidth - popup.width - 12))}px`,
    top: `${Math.max(12, Math.min(trigger.top - popup.height - 8, window.innerHeight - popup.height - 12))}px`
  }
}

async function toggleProfile() {
  if (profileOpen.value) { closeProfile(); return }
  collapsedGroupKey.value = null
  profileOpen.value = true
  await nextTick()
  positionProfile()
  profileMenu.value?.querySelector<HTMLButtonElement>('button')?.focus()
}

function onProfileFocusOut(event: FocusEvent) {
  const nextTarget = event.relatedTarget as Node | null
  if (!profileMenu.value?.contains(nextTarget) && !profileTrigger.value?.contains(nextTarget)) closeProfile()
}

function dismissProfile(event: PointerEvent) {
  const target = event.target as Node | null
  if (!profileMenu.value?.contains(target) && !profileTrigger.value?.contains(target)) closeProfile()
}

function handleShellKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    if (profileOpen.value) { event.preventDefault(); closeProfile(true) }
    else if (mobileNavigationOpen.value) { event.preventDefault(); void closeMobileNavigation() }
    else collapsedGroupKey.value = null
    return
  }
  if (event.key !== 'Tab' || !mobileNavigationOpen.value) return
  const elements = [
    ...Array.from(sidebarElement.value?.querySelectorAll<HTMLElement>('a[href], button:not([disabled])') ?? []),
    ...Array.from(profileMenu.value?.querySelectorAll<HTMLElement>('button:not([disabled])') ?? [])
  ].filter(element => element.getClientRects().length)
  const first = elements[0]
  const last = elements[elements.length - 1]
  if (!first || !last) return
  if (event.shiftKey && (document.activeElement === first || !elements.includes(document.activeElement as HTMLElement))) {
    event.preventDefault(); last.focus()
  } else if (!event.shiftKey && (document.activeElement === last || !elements.includes(document.activeElement as HTMLElement))) {
    event.preventDefault(); first.focus()
  }
}

function syncViewport() {
  isMobile.value = mobileViewport.matches
  collapsedGroupKey.value = null
  closeProfile()
  void closeMobileNavigation(false)
}

async function handleLogout() {
  closeProfile()
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

onMounted(() => {
  void loadOrganization()
  mobileViewport.addEventListener('change', syncViewport)
  document.addEventListener('pointerdown', dismissProfile)
  document.addEventListener('keydown', handleShellKeydown)
  window.addEventListener('resize', positionProfile)
})

onBeforeUnmount(() => {
  mobileViewport.removeEventListener('change', syncViewport)
  document.removeEventListener('pointerdown', dismissProfile)
  document.removeEventListener('keydown', handleShellKeydown)
  window.removeEventListener('resize', positionProfile)
  if (previousBodyOverflow !== undefined) document.body.style.overflow = previousBodyOverflow
})

watch(mobileNavigationOpen, open => {
  if (open) {
    previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
  } else if (previousBodyOverflow !== undefined) {
    document.body.style.overflow = previousBodyOverflow
    previousBodyOverflow = undefined
  }
})

watch(authSessionKey, (nextKey) => {
  if (seenAuthSessionKey && nextKey !== seenAuthSessionKey) {
    navigation.resetForSession()
    openGroups.value.clear()
    collapsedGroupKey.value = null
    profileOpen.value = false
    void closeMobileNavigation(false)
    if (nextKey && route.name !== 'login') void router.replace('/dashboard')
  }
  seenAuthSessionKey = nextKey
  if (nextKey) void loadOrganization()
})

watch(() => route.fullPath, () => {
  collapsedGroupKey.value = null
  closeProfile()
  void closeMobileNavigation()
  navigation.activate(applicationTabRoute(route.fullPath))
  items.value.filter(item => !item.route && groupActive(item)).forEach(item => openGroups.value.add(item.key))
})
watch(() => navigation.collapsed, collapsed => {
  if (!collapsed) collapsedGroupKey.value = null
  closeProfile()
})
watch(() => route.name, () => { loadOrganization() })
</script>
