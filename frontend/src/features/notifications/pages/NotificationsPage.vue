<template>
  <div><div class="toolbar page-toolbar"><strong class="cell-primary">Thông báo</strong><span class="cell-subtle">{{ unread }} chưa đọc</span></div><div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div><section class="panel"><div class="panel-heading"><h2>Hộp thư</h2><span>{{ unread }} chưa đọc</span></div><div v-if="loading" class="loading-state">Đang tải thông báo…</div><div v-else-if="notifications.length" class="list-compact"><article v-for="item in notifications" :key="item.id" class="list-compact-item" :style="{ opacity: item.readAt ? .65 : 1 }"><div><strong>{{ item.title }}</strong><span>{{ item.body }} · {{ formatDateTime(item.createdAt) }}</span></div><button v-if="!item.readAt" class="mini-button" @click="read(item)">Đánh dấu đã đọc</button><StatusPill v-else value="Đã đọc" tone="success" /></article></div><div v-else class="empty-state"><strong>Không có thông báo</strong><span>Bạn sẽ thấy thông báo lớp học và đánh giá tại đây.</span></div></section></div>
</template>
<script setup lang="ts">
import { errorMessage } from '../../../shared/api/http'
import { computed, onMounted, ref } from 'vue'; import StatusPill from '../../../shared/ui/StatusPill.vue'; import { communicationApi } from '../../../shared/api/client'; import { formatDateTime, type Notification } from '../../../shared/types'
const notifications = ref<Notification[]>([]); const loading = ref(true); const error = ref(''); const unread = computed(() => notifications.value.filter(item => !item.readAt).length)
async function load() { loading.value = true; try { notifications.value = await communicationApi.notifications() } catch (cause) { error.value = errorMessage(cause) || 'Không tải được thông báo.' } finally { loading.value = false } }
async function read(item: Notification) { try { await communicationApi.markRead(item.id); item.readAt = new Date().toISOString() } catch (cause) { error.value = errorMessage(cause) || 'Không cập nhật được thông báo.' } }
onMounted(load)
</script>
