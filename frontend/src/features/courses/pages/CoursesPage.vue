<template>
  <div>
    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <section v-if="showForm" class="panel" style="margin-bottom:16px"><form class="inline-form" @submit.prevent="create"><div class="form-field"><label>Mã khóa học</label><input v-model.trim="form.code" class="form-input" required placeholder="SE101" /></div><div class="form-field"><label>Tên khóa học</label><input v-model.trim="form.title" class="form-input" required placeholder="Kỹ thuật phần mềm" /></div><div class="form-field"><label>Mô tả</label><input v-model.trim="form.description" class="form-input" placeholder="Mô tả ngắn" /></div><button class="button primary" :disabled="busy">Lưu</button></form></section>
    <div class="toolbar page-toolbar"><input v-model="query" class="search-input" placeholder="Tìm theo mã hoặc tên khóa học…" /><div class="toolbar-actions"><span class="cell-subtle">{{ filtered.length }} khóa học</span><button v-if="canManage" class="button primary" @click="showForm = !showForm">{{ showForm ? 'Đóng biểu mẫu' : 'Tạo khóa học' }}</button></div></div>
    <div v-if="loading" class="loading-state">Đang tải khóa học…</div>
    <div v-else-if="filtered.length" class="cards-grid">
      <article v-for="course in filtered" :key="course.id" class="course-card"><div class="card-topline"><span class="code-label">{{ course.code }}</span><StatusPill :value="course.status" :labels="enumLabels.courseStatus" /></div><h3>{{ course.title }}</h3><p>{{ course.description || 'Chưa có mô tả cho khóa học này.' }}</p><div class="card-footer"><span class="card-meta">{{ course.createdAt ? formatDate(course.createdAt) : '—' }}</span><div class="card-actions"><RouterLink class="mini-button" :to="`/courses/${course.id}`">Chi tiết</RouterLink><button v-if="canManage && isCourseDraft(course.status)" class="mini-button" @click="publish(course.id)">Phát hành</button><button v-if="canManage && isCoursePublished(course.status)" class="mini-button" @click="archive(course.id)">Lưu trữ</button></div></div></article>
    </div>
    <div v-else class="panel empty-state"><strong>Chưa có khóa học phù hợp</strong><span>Tạo khóa học mới hoặc thay đổi từ khóa tìm kiếm.</span></div>
  </div>
</template>

<script setup lang="ts">
import { errorMessage } from '../../../shared/api/http'
import { computed, onMounted, reactive, ref } from 'vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import { academicsApi } from '../../../shared/api/client'
import { useAuthStore } from '../../../shared/stores/auth'
import { enumLabels, formatDate, type Course } from '../../../shared/types'

const auth = useAuthStore(); const courses = ref<Course[]>([]); const query = ref(''); const loading = ref(true); const busy = ref(false); const error = ref(''); const showForm = ref(false)
const form = reactive({ code: '', title: '', description: '' })
const canManage = computed(() => auth.has('academics.courses.manage'))
function isCourseDraft(status: string | number) { return status === 0 || String(status).toLowerCase() === 'draft' }
function isCoursePublished(status: string | number) { return status === 1 || String(status).toLowerCase() === 'published' }
const filtered = computed(() => courses.value.filter(item => `${item.code} ${item.title}`.toLowerCase().includes(query.value.toLowerCase())))
async function load() { loading.value = true; error.value = ''; try { courses.value = await academicsApi.courses() } catch (cause) { error.value = errorMessage(cause) || 'Không tải được khóa học.' } finally { loading.value = false } }
async function create() { busy.value = true; try { const created = await academicsApi.createCourse(form); courses.value.unshift(created); form.code = ''; form.title = ''; form.description = ''; showForm.value = false } catch (cause) { error.value = errorMessage(cause) || 'Không tạo được khóa học.' } finally { busy.value = false } }
async function publish(id: string) { try { const updated = await academicsApi.publishCourse(id); const index = courses.value.findIndex(item => item.id === id); if (index >= 0) courses.value[index] = updated } catch (cause) { error.value = errorMessage(cause) || 'Không phát hành được khóa học.' } }
async function archive(id: string) { if (!window.confirm('Lưu trữ khóa học này?')) return; try { const updated = await academicsApi.archiveCourse(id); const index = courses.value.findIndex(item => item.id === id); if (index >= 0) courses.value[index] = updated } catch (cause) { error.value = errorMessage(cause) || 'Không lưu trữ được khóa học.' } }
onMounted(load)
</script>
