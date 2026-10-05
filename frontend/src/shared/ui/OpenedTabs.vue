<template>
  <div class="opened-tabs">
    <div ref="tabStrip" class="tab-strip" role="tablist" aria-label="Các trang đang mở">
      <div v-for="tab in navigation.openedTabs" :key="tab.key" class="opened-tab" :class="{ active: tab.route === navigation.activeRoute }" :data-tab-key="tab.key" role="presentation">
        <button type="button" class="opened-tab-activate" role="tab" :aria-selected="tab.route === navigation.activeRoute" aria-controls="workspace-page" :tabindex="tab.route === navigation.activeRoute ? 0 : -1" @click="go(tab.route)" @keydown="onTabKeydown($event, tab.key)">{{ tab.title }}</button>
        <button v-if="tab.closable" type="button" class="tab-close" :aria-label="`Đóng tab ${tab.title}`" :title="`Đóng tab ${tab.title}`" @click="close(tab.key)">×</button>
      </div>
    </div>
    <button v-if="navigation.openedTabs.length > 1" type="button" class="tabs-menu" title="Đóng các tab khác" aria-label="Đóng các tab khác" @click="closeOthers">•••</button>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useNavigationStore } from '../stores/navigation'

const router = useRouter()
const navigation = useNavigationStore()
const tabStrip = ref<HTMLElement | null>(null)
let resizeObserver: ResizeObserver | undefined

function tabElement(key: string) {
  return Array.from(tabStrip.value?.querySelectorAll<HTMLElement>('.opened-tab') ?? []).find(element => element.dataset.tabKey === key)
}

async function revealActiveTab() {
  await nextTick()
  const strip = tabStrip.value
  const active = navigation.openedTabs.find(tab => tab.route === navigation.activeRoute)
  if (!strip || !active) return
  const tabBounds = tabElement(active.key)?.getBoundingClientRect()
  if (!tabBounds) return
  const stripBounds = strip.getBoundingClientRect()
  if (tabBounds.right - tabBounds.left > stripBounds.right - stripBounds.left) strip.scrollLeft += tabBounds.left - stripBounds.left
  else if (tabBounds.left < stripBounds.left) strip.scrollLeft += tabBounds.left - stripBounds.left
  else if (tabBounds.right > stripBounds.right) strip.scrollLeft += tabBounds.right - stripBounds.right
}

function focusTab(key: string) { tabElement(key)?.querySelector<HTMLButtonElement>('.opened-tab-activate')?.focus({ preventScroll: true }) }
async function go(path: string) { await router.push(path) }

async function close(key: string) {
  const target = navigation.openedTabs.find(tab => tab.key === key)
  if (!target?.closable) return
  const index = navigation.openedTabs.indexOf(target)
  const restoreFocus = tabElement(key)?.contains(document.activeElement) ?? false
  const wasActive = target?.route === navigation.activeRoute
  navigation.closeTab(key)
  if (wasActive) await router.push(navigation.activeRoute)
  await nextTick()
  if (restoreFocus) {
    const next = wasActive
      ? navigation.openedTabs.find(tab => tab.route === navigation.activeRoute)
      : navigation.openedTabs[Math.min(index, navigation.openedTabs.length - 1)]
    if (next) focusTab(next.key)
  }
}

async function closeOthers() {
  const current = navigation.openedTabs.find(tab => tab.route === navigation.activeRoute)
  if (!current) return
  navigation.closeOthers(current.key)
  await nextTick()
  focusTab(current.key)
}

async function onTabKeydown(event: KeyboardEvent, key: string) {
  const index = navigation.openedTabs.findIndex(tab => tab.key === key)
  if (index < 0) return
  if (event.key === 'Delete') { event.preventDefault(); await close(key); return }
  const last = navigation.openedTabs.length - 1
  const nextIndex = event.key === 'ArrowRight' ? (index + 1) % (last + 1)
    : event.key === 'ArrowLeft' ? (index + last) % (last + 1)
    : event.key === 'Home' ? 0 : event.key === 'End' ? last : undefined
  if (nextIndex === undefined) return
  event.preventDefault()
  const target = navigation.openedTabs[nextIndex]
  await go(target.route)
  await nextTick()
  focusTab(target.key)
}

watch(() => [navigation.activeRoute, ...navigation.openedTabs.map(tab => `${tab.key}:${tab.title}`)], revealActiveTab, { flush: 'post', immediate: true })
onMounted(() => {
  if (typeof ResizeObserver !== 'undefined' && tabStrip.value) {
    resizeObserver = new ResizeObserver(() => { void revealActiveTab() })
    resizeObserver.observe(tabStrip.value)
  }
  void revealActiveTab()
})
onBeforeUnmount(() => resizeObserver?.disconnect())
</script>
