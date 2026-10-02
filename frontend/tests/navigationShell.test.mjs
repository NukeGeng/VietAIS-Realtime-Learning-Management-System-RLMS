import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'
import { computed, effectScope, markRaw, nextTick, reactive, ref, watch } from 'vue'

function evaluate(path, exports, context) {
  const source = readFileSync(new URL(path, import.meta.url), 'utf8')
  const script = (source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)?.[1] ?? source).replace(/^import .*$/gm, '').replace(/^export /gm, '')
  return vm.runInNewContext(`${ts.transpile(script, { target: ts.ScriptTarget.ES2022 })}\n;({ ${exports} })`, context)
}

function setup(path, exports, mobile = false) {
  const scope = effectScope()
  const mounts = [], unmounts = [], pushes = []
  const document = { activeElement: null, body: { style: { overflow: 'auto' } }, addEventListener() {}, removeEventListener() {} }
  const viewport = { matches: mobile, addEventListener() {}, removeEventListener() {} }
  const route = reactive({ name: 'dashboard', path: '/dashboard', fullPath: '/dashboard', meta: {} })
  const context = { ref, computed, watch, nextTick, document, window: { matchMedia: () => viewport, innerWidth: 1280, innerHeight: 800, addEventListener() {}, removeEventListener() {} }, onMounted: callback => mounts.push(callback), onBeforeUnmount: callback => unmounts.push(callback) }
  const { useNavigationStore, applicationTabRoute } = evaluate('../src/shared/stores/navigation.ts', 'useNavigationStore, applicationTabRoute', { ...context, P: {}, defineStore: (_name, factory) => () => reactive(factory()) })
  const navigation = useNavigationStore()
  const router = { async push(path) { pushes.push(path); navigation.activate(path) }, replace() {} }
  Object.assign(context, { applicationTabRoute, useNavigationStore: () => navigation, useRoute: () => route, useRouter: () => router, useAuthStore: () => ({ account: null, role: 'student', permissions: new Set(), isAuthenticated: false }), identityApi: {}, errorMessage: String })
  const state = scope.run(() => evaluate(path, exports, context))
  return { state, navigation, document, route, viewport, context, mounts, pushes, dispose() { unmounts.forEach(callback => callback()); scope.stop() } }
}

function control(document) {
  return markRaw({ focus() { document.activeElement = this }, getClientRects: () => [{}], contains(target) { return target === this } })
}

function tabs() {
  const runtime = setup('../src/shared/ui/OpenedTabs.vue', 'tabStrip, close, closeOthers, onTabKeydown, revealActiveTab')
  const { state, navigation, document } = runtime
  navigation.addTab({ key: 'classes', title: 'Lớp học', route: '/classes', closable: true })
  navigation.addTab({ key: 'calendar', title: 'Lịch học', route: '/calendar', closable: true })
  const strip = markRaw({ scrollLeft: 0, width: 320, getBoundingClientRect() { return { left: 0, right: this.width } } })
  const elements = new Map(navigation.openedTabs.map((tab, index) => {
    const activate = control(document), close = control(document)
    return [tab.key, markRaw({ dataset: { tabKey: tab.key }, activate, close, left: index * 200, width: 200,
      getBoundingClientRect() { return { left: this.left - strip.scrollLeft, right: this.left + this.width - strip.scrollLeft } },
      querySelector: () => activate, contains: target => target === activate || target === close })]
  }))
  strip.querySelectorAll = () => navigation.openedTabs.map(tab => elements.get(tab.key))
  state.tabStrip.value = strip
  return { ...runtime, strip, elements }
}

function key(name, shiftKey = false) {
  return { key: name, shiftKey, prevented: false, preventDefault() { this.prevented = true } }
}

const flushUi = () => new Promise(resolve => setImmediate(resolve))

test('class tabs use short labels while the header follows the full class context', t => {
  const runtime = setup('../src/app/layouts/AppShell.vue', 'currentTitle')
  t.after(runtime.dispose)
  const { navigation, state } = runtime
  navigation.addTab({ key: 'class-a', title: 'Lớp học', route: '/classes/class-a', closable: true })
  navigation.renameTab('/classes/class-a/essays', 'SE01', 'SE01 · C# Backend')
  navigation.activate('/classes/class-a/content')
  assert.equal(navigation.activeTab.title, 'SE01')
  assert.equal(state.currentTitle.value, 'SE01 · C# Backend')

  navigation.addTab({ key: 'class-b', title: 'Lớp học', route: '/classes/class-b', closable: true })
  navigation.renameTab('/classes/class-b/overview', 'SE02', 'SE02 · ASP.NET Core')
  assert.equal(state.currentTitle.value, 'SE02 · ASP.NET Core')
  navigation.activate('/classes/class-a/resources')
  assert.equal(state.currentTitle.value, 'SE01 · C# Backend')
  assert.equal(navigation.openedTabs.length, 3, 'class subtabs reuse their application tab')
  navigation.activate('/classes/class-b/essays')
  navigation.closeTab('class-b')
  assert.equal(state.currentTitle.value, 'SE01 · C# Backend', 'closing a class restores the neighboring header')
  navigation.renameTab('/classes/class-a', 'Tên trang mới')
  assert.equal(state.currentTitle.value, 'Tên trang mới', 'a normal rename clears the optional header title')
})

test('Delete closes the active tab, routes to its neighbor and restores keyboard focus', async t => {
  const runtime = tabs(); t.after(runtime.dispose)
  runtime.elements.get('calendar').activate.focus()
  const event = key('Delete')
  await runtime.state.onTabKeydown(event, 'calendar')
  assert.equal(event.prevented, true)
  assert.equal(runtime.navigation.activeRoute, '/classes')
  assert.deepEqual(runtime.pushes, ['/classes'])
  assert.equal(runtime.document.activeElement, runtime.elements.get('classes').activate)
  await runtime.state.onTabKeydown(key('Delete'), 'dashboard')
  assert.equal(runtime.navigation.openedTabs[0].key, 'dashboard')
})

test('closing a focused inactive tab preserves the page and gives focus to a surviving control', async t => {
  const runtime = tabs(); t.after(runtime.dispose)
  runtime.elements.get('classes').close.focus()
  await runtime.state.close('classes')
  assert.equal(runtime.navigation.activeRoute, '/calendar')
  assert.deepEqual(runtime.pushes, [])
  assert.equal(runtime.document.activeElement, runtime.elements.get('calendar').activate)
})

test('arrow keys wrap across tabs, and Home/End navigate with focus', async t => {
  const runtime = tabs(); t.after(runtime.dispose)
  await runtime.state.onTabKeydown(key('ArrowRight'), 'calendar')
  assert.equal(runtime.navigation.activeRoute, '/dashboard')
  assert.equal(runtime.document.activeElement, runtime.elements.get('dashboard').activate)
  await runtime.state.onTabKeydown(key('End'), 'dashboard')
  assert.equal(runtime.navigation.activeRoute, '/calendar')
  await runtime.state.onTabKeydown(key('Home'), 'calendar')
  assert.equal(runtime.navigation.activeRoute, '/dashboard')
})

test('active tabs are revealed after selection, dynamic title growth, and container resize', async t => {
  const runtime = tabs(); t.after(runtime.dispose)
  await runtime.state.revealActiveTab()
  assert.equal(runtime.strip.scrollLeft, 280)
  runtime.elements.get('calendar').width = 260
  runtime.navigation.renameTab('/calendar', 'Lịch học tháng mười')
  await flushUi()
  assert.equal(runtime.strip.scrollLeft, 340)
  let resized
  runtime.context.ResizeObserver = class { constructor(callback) { resized = callback } observe() {} disconnect() {} }
  runtime.mounts.forEach(callback => callback())
  runtime.strip.width = 280
  resized()
  await flushUi()
  assert.equal(runtime.strip.scrollLeft, 380)
  runtime.strip.width = 240
  resized()
  await flushUi()
  assert.equal(runtime.strip.scrollLeft, 400, 'a tab wider than the strip keeps its title visible without oscillating')
  runtime.navigation.activate('/dashboard')
  await flushUi()
  assert.equal(runtime.strip.scrollLeft, 0)
})

function shell(mobile = true) {
  const runtime = setup('../src/app/layouts/AppShell.vue', 'isMobile, sidebarCollapsed, mobileNavigationOpen, sidebarElement, mobileNavigationTrigger, profileTrigger, profileMenu, profileOpen, profilePosition, openMobileNavigation, closeMobileNavigation, toggleProfile, handleShellKeydown, syncViewport, toggleGroup, openGroups, collapsedGroupKey', mobile)
  const first = control(runtime.document), last = control(runtime.document), trigger = control(runtime.document)
  runtime.state.sidebarElement.value = markRaw({ querySelector: () => first, querySelectorAll: () => [first, last], getBoundingClientRect: () => ({ right: 78 }) })
  runtime.state.mobileNavigationTrigger.value = trigger
  return { ...runtime, first, last, trigger }
}

test('mobile drawer opens with focus and closes on route change, restoring body scroll and trigger focus', async t => {
  const runtime = shell(); t.after(runtime.dispose)
  await runtime.state.openMobileNavigation()
  assert.equal(runtime.document.activeElement, runtime.first, 'initial drawer focus')
  assert.equal(runtime.document.body.style.overflow, 'hidden')
  runtime.route.path = '/calendar'; runtime.route.fullPath = '/calendar'
  await flushUi()
  assert.equal(runtime.state.mobileNavigationOpen.value, false)
  assert.equal(runtime.document.body.style.overflow, 'auto')
  assert.equal(runtime.document.activeElement, runtime.trigger, 'drawer focus after route change')
})

test('mobile drawer keyboard focus stays inside and Escape returns to the menu toggle', async t => {
  const runtime = shell(); t.after(runtime.dispose)
  await runtime.state.openMobileNavigation()
  runtime.last.focus()
  const tab = key('Tab'); runtime.state.handleShellKeydown(tab)
  assert.equal(tab.prevented, true)
  assert.equal(runtime.document.activeElement, runtime.first, 'Tab wraps to first drawer control')
  runtime.state.handleShellKeydown(key('Tab', true))
  assert.equal(runtime.document.activeElement, runtime.last, 'Shift+Tab wraps to last drawer control')
  runtime.state.handleShellKeydown(key('Escape'))
  await flushUi()
  assert.equal(runtime.state.mobileNavigationOpen.value, false)
  assert.equal(runtime.document.activeElement, runtime.trigger, 'Escape returns focus to toggle')
})

test('mobile groups stay expanded independently of the saved desktop collapse state', t => {
  const runtime = shell(); t.after(runtime.dispose)
  runtime.navigation.collapsed = true
  assert.equal(runtime.state.sidebarCollapsed.value, false)
  runtime.state.toggleGroup('learning')
  assert.equal(runtime.state.openGroups.value.has('learning'), true)
  assert.equal(runtime.state.collapsedGroupKey.value, null)
  runtime.viewport.matches = false; runtime.state.syncViewport()
  assert.equal(runtime.state.sidebarCollapsed.value, true)
  assert.equal(runtime.navigation.collapsed, true)
})

test('collapsed account popup sits outside the sidebar, remains within the viewport, and returns focus on Escape', async t => {
  const runtime = shell(false); t.after(runtime.dispose)
  runtime.navigation.collapsed = true
  await flushUi()
  const logout = control(runtime.document), profile = control(runtime.document)
  profile.getBoundingClientRect = () => ({ left: 20, right: 66, top: 600 })
  runtime.state.profileTrigger.value = profile
  runtime.state.profileMenu.value = markRaw({ getBoundingClientRect: () => ({ width: 195, height: 80 }), querySelector: () => logout })
  await runtime.state.toggleProfile()
  assert.equal(runtime.state.profilePosition.value.left, '86px')
  assert.equal(runtime.state.profilePosition.value.top, '512px')
  assert.equal(runtime.document.activeElement, logout)
  runtime.state.handleShellKeydown(key('Escape'))
  assert.equal(runtime.state.profileOpen.value, false)
  assert.equal(runtime.document.activeElement, profile, 'Escape returns focus to profile')
  runtime.context.window.innerWidth = 240; runtime.context.window.innerHeight = 100
  await runtime.state.toggleProfile()
  assert.equal(runtime.state.profilePosition.value.left, '33px')
  assert.equal(runtime.state.profilePosition.value.top, '12px')
})
