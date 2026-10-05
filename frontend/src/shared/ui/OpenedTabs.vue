<template>
  <div class="opened-tabs" role="tablist" aria-label="Các trang đang mở">
    <div class="tab-strip">
      <button v-for="tab in navigation.openedTabs" :key="tab.key" class="opened-tab" :class="{ active: tab.route === navigation.activeRoute }" role="tab" @click="go(tab.route)">
        <span>{{ tab.title }}</span>
        <span v-if="tab.closable" class="tab-close" aria-label="Đóng tab" @click.stop="close(tab.key)">×</span>
      </button>
    </div>
    <button v-if="navigation.openedTabs.length > 1" class="tabs-menu" title="Đóng các tab khác" @click="closeOthers">•••</button>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useNavigationStore } from '../stores/navigation'

const router = useRouter()
const navigation = useNavigationStore()
function go(path: string) { navigation.activate(path); router.push(path) }
function close(key: string) {
  const target = navigation.openedTabs.find(tab => tab.key === key)
  const wasActive = target?.route === navigation.activeRoute
  navigation.closeTab(key)
  if (wasActive) router.push(navigation.activeRoute)
}
function closeOthers() {
  const current = navigation.openedTabs.find(tab => tab.route === navigation.activeRoute)
  if (current) navigation.closeOthers(current.key)
}
</script>
