<template>
  <div>
    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <TeacherClassSelector v-if="!selectedClassId" :classes="classes" :loading="loading" title="Chấm bài" description="Chọn một lớp để xem các hoạt động và bài nộp cần chấm." feature-count-label="hoạt động" @select="selectClass" />
    <template v-else>
      <div class="toolbar page-toolbar"><div class="class-context"><strong>{{ selectedClassLabel }}</strong><button class="mini-button" @click="changeClass">Đổi lớp</button></div><div class="toolbar-actions"><span class="cell-subtle">{{ activities.length }} hoạt động</span><RouterLink class="button secondary" to="/gradebook">Mở sổ điểm</RouterLink></div></div>
      <section v-if="loading" class="loading-state">Đang tải hoạt động cần chấm…</section>
      <section v-else-if="activities.length" class="panel"><div class="panel-heading"><h2>Hoạt động theo lớp</h2><span>Tổng quan và chi tiết được tải theo màn hình</span></div><div class="list-compact"><div v-for="activity in activities" :key="activity.id" class="list-compact-item"><div><strong>{{ activity.title }}</strong><span>{{ enumLabel(activity.kind, enumLabels.activityKind) }} · {{ activity.questions.length }} câu · {{ activity.dueAt ? `Hạn ${formatDate(activity.dueAt)}` : 'Không có hạn' }}</span></div><div class="card-actions"><StatusPill :value="activity.status" :labels="enumLabels.activityStatus" /><RouterLink class="mini-button" :to="`/activities/${activity.id}`">Mở chấm</RouterLink></div></div></div></section>
      <section v-else class="panel empty-state"><strong>Chưa có hoạt động</strong><span>Chọn lớp có hoạt động đã tạo để bắt đầu chấm bài.</span></section>
    </template>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import TeacherClassSelector from '../../../shared/ui/TeacherClassSelector.vue'
import { academicsApi, assessmentApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import { enumLabels, enumLabel, formatDate, type AssessmentActivity, type LearningClass } from '../../../shared/types'

const classes = ref<LearningClass[]>([])
const activities = ref<AssessmentActivity[]>([])
const selectedClassId = ref('')
const loading = ref(true)
const error = ref('')
const selectedClassLabel = computed(() => { const item = classes.value.find(entry => entry.id === selectedClassId.value); return item ? `${item.code} · ${item.name}` : 'Lớp học' })
function selectClass(id: string) { selectedClassId.value = id }
function changeClass() { selectedClassId.value = '' }

async function load() {
  if (!selectedClassId.value) { activities.value = []; loading.value = false; return }
  loading.value = true
  error.value = ''
  try { activities.value = await assessmentApi.activities(selectedClassId.value) } catch (cause) { error.value = errorMessage(cause) || 'Không tải được hoạt động.' } finally { loading.value = false }
}
async function boot() {
  try { classes.value = await academicsApi.classes(); loading.value = false } catch (cause) { error.value = errorMessage(cause) || 'Không tải được lớp học.'; loading.value = false }
}
watch(selectedClassId, load)
onMounted(boot)
</script>
