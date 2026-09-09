<template>
  <div>
    <PageHeader eyebrow="ASSESSMENT" title="Chấm bài" description="Chọn hoạt động để mở overview lớp, xem submission on-demand và chấm phần tự luận theo rubric.">
      <template #actions><RouterLink class="button secondary" to="/gradebook">Mở sổ điểm</RouterLink></template>
    </PageHeader>
    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <div class="toolbar"><select v-model="selectedClassId" class="form-select" style="max-width:340px"><option value="">Chọn lớp học</option><option v-for="item in classes" :key="item.id" :value="item.id">{{ item.code }} · {{ item.name }}</option></select><span class="cell-subtle">{{ activities.length }} hoạt động</span></div>
    <section v-if="loading" class="loading-state">Đang tải hoạt động cần chấm…</section>
    <section v-else-if="activities.length" class="panel"><div class="panel-heading"><h2>Hoạt động theo lớp</h2><span>Overview và detail được tải theo màn hình</span></div><div class="list-compact"><div v-for="activity in activities" :key="activity.id" class="list-compact-item"><div><strong>{{ activity.title }}</strong><span>{{ enumLabel(activity.kind) }} · {{ activity.questions.length }} câu · {{ activity.dueAt ? `Hạn ${formatDate(activity.dueAt)}` : 'Không có hạn' }}</span></div><div class="card-actions"><StatusPill :value="activity.status" /><RouterLink class="mini-button" :to="`/activities/${activity.id}`">Mở chấm</RouterLink></div></div></div></section>
    <section v-else class="panel empty-state"><strong>Chưa có hoạt động</strong><span>Chọn lớp có hoạt động đã tạo để bắt đầu chấm bài.</span></section>
  </div>
</template>
<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import PageHeader from '../../../shared/ui/PageHeader.vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import { academicsApi, assessmentApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import { enumLabel, formatDate, type AssessmentActivity, type LearningClass } from '../../../shared/types'

const classes = ref<LearningClass[]>([])
const activities = ref<AssessmentActivity[]>([])
const selectedClassId = ref('')
const loading = ref(true)
const error = ref('')

async function load() {
  if (!selectedClassId.value) { activities.value = []; loading.value = false; return }
  loading.value = true
  error.value = ''
  try { activities.value = await assessmentApi.activities(selectedClassId.value) } catch (cause) { error.value = errorMessage(cause) || 'Không tải được hoạt động.' } finally { loading.value = false }
}
async function boot() {
  try { classes.value = await academicsApi.classes(); if (classes.value[0]) selectedClassId.value = classes.value[0].id } catch (cause) { error.value = errorMessage(cause) || 'Không tải được lớp học.'; loading.value = false }
}
watch(selectedClassId, load)
onMounted(boot)
</script>
