<template>
  <div class="course-content-page">
    <div class="course-content-toolbar">
      <span v-if="content" class="course-content-toolbar-summary">{{ content.curriculumTitle }} · {{ content.modules.length }} mô-đun · {{ content.totalLessons }} bài học</span>
      <RouterLink class="button secondary" :to="`/classes/${classId}/content`">Về nội dung lớp</RouterLink>
    </div>

    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <div v-if="loading" class="loading-state">Đang tải nội dung khóa học…</div>
    <template v-else-if="content">
      <div class="course-content-layout">
        <nav class="course-curriculum" aria-label="Chương học">
          <div class="course-curriculum-header"><h2>Chương học</h2><span v-if="content.totalLessons">{{ content.completedLessons }}/{{ content.totalLessons }} bài · {{ content.progress }}%</span><span v-else>Chưa có bài học</span></div>
          <div v-if="content.modules.length" class="course-modules">
            <section v-for="module in content.modules" :key="module.moduleId" class="course-module">
              <button class="course-module-toggle" :aria-expanded="isOpen(module.moduleId)" @click="toggleModule(module.moduleId)">
                <span class="course-module-chevron">{{ isOpen(module.moduleId) ? '⌄' : '›' }}</span>
                <span class="course-module-copy"><strong>{{ module.title }}</strong><small v-if="module.totalLessons">{{ module.completedLessons }}/{{ module.totalLessons }} bài hoàn thành</small><small v-else>Chưa có bài học</small></span>
                <span v-if="module.totalLessons" class="course-module-percent">{{ Math.round(module.completedLessons / module.totalLessons * 100) }}%</span>
              </button>
              <div v-if="isOpen(module.moduleId)" class="course-lessons">
                <button v-for="lessonItem in module.lessons" :key="lessonItem.lessonId" class="course-lesson" :class="{ active: selectedLessonId === lessonItem.lessonId }" @click="openLesson(lessonItem)">
                  <span class="course-lesson-status" :class="statusClass(lessonItem.status)">{{ statusIcon(lessonItem.status) }}</span>
                  <span>{{ lessonItem.title }}</span>
                  <span v-if="selectedLessonId === lessonItem.lessonId" class="course-lesson-current">Đang xem</span>
                </button>
                <p v-if="!module.lessons.length" class="course-empty-lessons">Chưa có bài học được phát hành.</p>
              </div>
            </section>
          </div>
          <p v-else class="course-curriculum-empty">Chưa có nội dung được phát hành.</p>
        </nav>

        <section class="course-lesson-panel">
          <div v-if="lessonLoading" class="loading-state">Đang tải bài học…</div>
          <template v-else-if="lesson">
            <div class="course-lesson-header"><div><span class="course-detail-kicker">{{ lesson.moduleTitle }}</span><h2>{{ lesson.title }}</h2></div><span class="course-lesson-status-label">{{ statusLabel(lesson.status) }}</span></div>
            <div class="course-lesson-content"><p>{{ lesson.contentSummary || 'Bài học chưa có phần mô tả.' }}</p></div>
            <div v-if="lesson.resources.length" class="course-lesson-resources"><h3>Tài nguyên bài học</h3><a v-for="resource in lesson.resources" :key="resource.id" class="course-resource-row" :href="resource.accessUrl || resource.externalUrl || undefined" target="_blank" rel="noreferrer"><span><strong>{{ resource.title }}</strong><small>{{ resource.fileName || resource.description || enumLabel(resource.type) }}</small></span><span aria-hidden="true">↗</span></a></div>
            <div v-else class="course-lesson-resources course-lesson-resources-empty"><h3>Tài nguyên bài học</h3><span>Chưa có tài nguyên đính kèm.</span></div>
            <div class="course-lesson-actions"><span v-if="lessonMessage" class="cell-subtle">{{ lessonMessage }}</span><button class="button primary" :disabled="progressBusy || isCompleted(lesson.status)" @click="completeLesson">{{ isCompleted(lesson.status) ? 'Đã hoàn thành' : progressBusy ? 'Đang lưu…' : 'Đánh dấu hoàn thành' }}</button></div>
          </template>
          <p v-else class="course-lesson-placeholder">Chọn một bài học từ danh sách bên trái để bắt đầu học.</p>
        </section>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { learningApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import { enumLabel, type StudentCurriculumContent, type StudentLessonDetail, type StudentLessonSummary } from '../../../shared/types'
import { useAuthStore } from '../../../shared/stores/auth'
import { useNavigationStore } from '../../../shared/stores/navigation'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const navigation = useNavigationStore()
const classId = String(route.params.classId)
const curriculumId = String(route.params.curriculumId)
const content = ref<StudentCurriculumContent | null>(null)
const lesson = ref<StudentLessonDetail | null>(null)
const selectedLessonId = ref('')
const openModules = ref<string[]>([])
const loading = ref(true)
const lessonLoading = ref(false)
const progressBusy = ref(false)
const error = ref('')
const lessonMessage = ref('')

async function load() {
  if (auth.role !== 'student') {
    await router.replace(`/classes/${classId}`)
    return
  }
  loading.value = true
  error.value = ''
  try {
    const snapshot = await learningApi.curriculumContent(classId, curriculumId)
    content.value = snapshot
    navigation.renameTab(route.fullPath, snapshot.curriculumTitle)
    if (!openModules.value.length && snapshot.modules.length) openModules.value = [snapshot.modules[0].moduleId]
    if (selectedLessonId.value) {
      const selected = snapshot.modules.flatMap(module => module.lessons).find(item => item.lessonId === selectedLessonId.value)
      if (!selected) { selectedLessonId.value = ''; lesson.value = null }
    }
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải được nội dung khóa học.'
  } finally {
    loading.value = false
  }
}

function isOpen(moduleId: string) { return openModules.value.includes(moduleId) }
function toggleModule(moduleId: string) { openModules.value = isOpen(moduleId) ? openModules.value.filter(id => id !== moduleId) : [...openModules.value, moduleId] }
function statusText(status?: string | number | null) { return typeof status === 'number' ? ['InProgress', 'Completed'][status] || '' : status || '' }
function isCompleted(status?: string | number | null) { return status === 1 || statusText(status).toLowerCase() === 'completed' }
function statusClass(status?: string | number | null) { return isCompleted(status) ? 'completed' : statusText(status).toLowerCase() === 'inprogress' ? 'in-progress' : 'not-started' }
function statusIcon(status?: string | number | null) { return isCompleted(status) ? '✓' : statusClass(status) === 'in-progress' ? '●' : '○' }
function statusLabel(status?: string | number | null) { return isCompleted(status) ? 'Đã hoàn thành' : statusClass(status) === 'in-progress' ? 'Đang học' : 'Chưa bắt đầu' }

async function openLesson(item: StudentLessonSummary) {
  selectedLessonId.value = item.lessonId
  lessonLoading.value = true
  lessonMessage.value = ''
  try {
    if (item.status == null) {
      await learningApi.updateCurriculumLessonProgress(classId, curriculumId, item.lessonId, 0)
      await load()
    }
    lesson.value = await learningApi.curriculumLessonDetail(classId, curriculumId, item.lessonId)
  } catch (cause) {
    lessonMessage.value = errorMessage(cause) || 'Không tải được bài học.'
  } finally {
    lessonLoading.value = false
  }
}

async function completeLesson() {
  if (!lesson.value || isCompleted(lesson.value.status)) return
  progressBusy.value = true
  lessonMessage.value = ''
  try {
    const lessonId = lesson.value.lessonId
    await learningApi.updateCurriculumLessonProgress(classId, curriculumId, lessonId, 1)
    lessonMessage.value = 'Đã lưu tiến độ.'
    await load()
    lesson.value = await learningApi.curriculumLessonDetail(classId, curriculumId, lessonId)
  } catch (cause) {
    lessonMessage.value = errorMessage(cause) || 'Không cập nhật được tiến độ.'
  } finally {
    progressBusy.value = false
  }
}

onMounted(load)
</script>
