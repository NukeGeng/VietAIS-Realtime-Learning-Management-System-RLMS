<template>
  <div>
    <PageHeader eyebrow="CLASSROOM" :title="auth.role === 'student' ? 'Khóa học / Lớp học' : 'Lớp học'" description="Mở lớp để xem hoạt động, tài nguyên, lịch và trạng thái học tập.">
      <template #actions><button v-if="canManage" class="button primary" @click="showForm = !showForm">{{ showForm ? 'Đóng biểu mẫu' : 'Tạo lớp học' }}</button></template>
    </PageHeader>
    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <section v-if="showForm" class="panel" style="margin-bottom:16px"><form class="grid-two" @submit.prevent="create"><div class="form-field"><label>Khóa học</label><select v-model="form.courseId" class="form-select" required><option value="">Chọn khóa học</option><option v-for="course in courses" :key="course.id" :value="course.id">{{ course.code }} · {{ course.title }}</option></select></div><div class="form-field"><label>Mã lớp</label><input v-model.trim="form.code" class="form-input" required placeholder="SE01" /></div><div class="form-field"><label>Tên lớp</label><input v-model.trim="form.name" class="form-input" required placeholder="Software Engineering 01" /></div><div class="form-field"><label>Thời gian</label><div style="display:flex;gap:8px"><input v-model="form.startDate" class="form-input" type="date" required /><input v-model="form.endDate" class="form-input" type="date" required /></div></div><button class="button primary" :disabled="busy">Lưu lớp học</button></form></section>
    <div class="toolbar"><input v-model="query" class="search-input" placeholder="Tìm theo mã hoặc tên lớp…" /><span class="cell-subtle">{{ filtered.length }} lớp học</span></div>
    <div v-if="loading" class="loading-state">Đang tải lớp học…</div>
    <div v-else-if="filtered.length" class="cards-grid"><article v-for="item in filtered" :key="item.id" class="class-card"><div class="card-topline"><span class="code-label">{{ item.code }}</span><StatusPill :value="item.status" /></div><h3>{{ item.name }}</h3><p>{{ dateRange(item) }}</p><div class="card-footer"><span class="card-meta">Course {{ item.courseId.slice(0, 8) }}</span><RouterLink class="mini-button" :to="`/classes/${item.id}`">Mở lớp</RouterLink></div></article></div>
    <div v-else class="panel empty-state"><strong>Chưa có lớp học</strong><span>Lớp được tạo hoặc phân công sẽ hiển thị tại đây.</span></div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import PageHeader from '../../../shared/ui/PageHeader.vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import { academicsApi } from '../../../shared/api/client'
import { useAuthStore } from '../../../shared/stores/auth'
import { formatDate, type Course, type LearningClass } from '../../../shared/types'

const auth = useAuthStore(); const classes = ref<LearningClass[]>([]); const courses = ref<Course[]>([]); const query = ref(''); const loading = ref(true); const busy = ref(false); const error = ref(''); const showForm = ref(false)
const form = reactive({ courseId: '', code: '', name: '', startDate: '', endDate: '' })
const canManage = computed(() => auth.has('academics.classes.manage')); const filtered = computed(() => classes.value.filter(item => `${item.code} ${item.name}`.toLowerCase().includes(query.value.toLowerCase())))
function dateRange(item: LearningClass) { return `${formatDate(item.startDate)} – ${formatDate(item.endDate)}` }
async function load() { loading.value = true; error.value = ''; const results = await Promise.allSettled([academicsApi.classes(), academicsApi.courses()]); if (results[0].status === 'fulfilled') classes.value = results[0].value; if (results[1].status === 'fulfilled') courses.value = results[1].value; if (results[0].status === 'rejected') error.value = 'Không tải được lớp học.'; loading.value = false }
async function create() { busy.value = true; try { const created = await academicsApi.createClass(form); classes.value.unshift(created); showForm.value = false; form.courseId = ''; form.code = ''; form.name = ''; form.startDate = ''; form.endDate = '' } catch (cause) { error.value = String((cause as Error)?.message || 'Không tạo được lớp học.') } finally { busy.value = false } }
onMounted(load)
</script>
