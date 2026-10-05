<template>
  <div class="teacher-student-workspace-page">
    <div v-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button class="mini-button" @click="load">Thử lại</button>
    </div>
    <div v-if="loading" class="loading-state">Đang tải không gian làm bài của sinh viên…</div>
    <StudentAttemptWorkspace
      v-if="activity && detail"
      mode="observer"
      :activity="activity"
      :attempt="detail"
      :back-to="`/activities/${activityId}`"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useRoute } from 'vue-router'
import StudentAttemptWorkspace from '../components/StudentAttemptWorkspace.vue'
import { assessmentApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import type { AssessmentActivity, StudentDetail } from '../../../shared/types'
import { useNavigationStore } from '../../../shared/stores/navigation'

type ActivityDetailDelta = {
  attemptId: string
  version: number
  changeType: string
  questionId?: string
  answerValue?: string
  currentQuestionId?: string
  answeredCount: number
  status: string | number
  lastActivityAt: string
  helpRequested: boolean
}

const route = useRoute()
const navigation = useNavigationStore()
const activity = ref<AssessmentActivity | null>(null)
const detail = ref<StudentDetail | null>(null)
const connection = ref<HubConnection | null>(null)
const loading = ref(true)
const error = ref('')
let loadSequence = 0
const activityId = computed(() => String(route.params.activityId || ''))
const attemptId = computed(() => String(route.params.attemptId || ''))

function applyDelta(delta: ActivityDetailDelta) {
  const current = detail.value
  if (!current || delta.attemptId !== (current.attemptId || current.id) || delta.version < current.version) return
  const answers = { ...current.answers }
  if (delta.questionId && (delta.changeType === 'answer-changed' || delta.changeType === 'answer-submitted')) {
    answers[delta.questionId] = delta.answerValue || ''
  }
  detail.value = {
    ...current,
    answers,
    currentQuestionId: delta.currentQuestionId,
    answeredCount: delta.answeredCount,
    progress: Math.round(delta.answeredCount / Math.max(1, current.totalQuestions) * 100),
    status: delta.status,
    lastActivityAt: delta.lastActivityAt,
    helpRequested: delta.helpRequested,
    version: delta.version,
  }
}

async function connectRealtime(sequence: number, requestedAttemptId: string) {
  await connection.value?.stop().catch(() => undefined)
  const hub = new HubConnectionBuilder()
    .withUrl('/hubs/assessment')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  hub.on('activity-detail-delta', applyDelta)
  hub.onreconnected(async () => {
    if (sequence !== loadSequence || requestedAttemptId !== attemptId.value) return
    await hub.invoke('SubscribeStudentDetail', requestedAttemptId).catch(() => undefined)
    await refreshDetail()
  })
  try {
    await hub.start()
    if (sequence !== loadSequence || requestedAttemptId !== attemptId.value) {
      await hub.stop().catch(() => undefined)
      return
    }
    await hub.invoke('SubscribeStudentDetail', requestedAttemptId)
    if (sequence !== loadSequence || requestedAttemptId !== attemptId.value) {
      await hub.stop().catch(() => undefined)
      return
    }
    connection.value = hub
    await refreshDetail()
  } catch {
    await hub.stop().catch(() => undefined)
  }
}

async function refreshDetail() {
  if (!attemptId.value) return
  const sequence = loadSequence
  const requestedAttemptId = attemptId.value
  try {
    const snapshot = await assessmentApi.detail(requestedAttemptId)
    if (sequence !== loadSequence || requestedAttemptId !== attemptId.value) return
    if (!detail.value || snapshot.version >= detail.value.version) detail.value = snapshot
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải lại được không gian làm bài của sinh viên.'
  }
}

async function load() {
  if (!activityId.value || !attemptId.value) return
  const sequence = ++loadSequence
  await connection.value?.stop().catch(() => undefined)
  connection.value = null
  loading.value = true
  activity.value = null
  detail.value = null
  error.value = ''
  try {
    const [activitySnapshot, detailSnapshot] = await Promise.all([
      assessmentApi.activity(activityId.value),
      assessmentApi.detail(attemptId.value),
    ])
    if (sequence !== loadSequence) return
    activity.value = activitySnapshot
    detail.value = detailSnapshot
    navigation.renameTab(route.fullPath, activitySnapshot.title)
    await connectRealtime(sequence, attemptId.value)
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải được không gian làm bài của sinh viên.'
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

watch(() => [activityId.value, attemptId.value], load, { immediate: true })
onBeforeUnmount(async () => {
  await connection.value?.stop().catch(() => undefined)
})
</script>

<style scoped>
.teacher-student-workspace-page { width: 100%; }
.teacher-student-workspace-page > .error-banner, .teacher-student-workspace-page > .loading-state { margin-bottom: 16px; }
</style>
