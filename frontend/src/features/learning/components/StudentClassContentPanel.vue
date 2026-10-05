<template>
  <section class="student-class-content" aria-label="Chương trình học của lớp">
    <div v-if="!loading && (curriculums.length > 1 || search)" class="student-class-content-toolbar">
      <span class="cell-subtle">{{ curriculums.length }} chương trình học</span>
      <input v-if="curriculums.length > 1" v-model.trim="search" class="search-input" type="search" placeholder="Tìm chương trình học theo tên" aria-label="Tìm chương trình học" />
    </div>

    <div v-if="loading" class="loading-state">Đang tải chương trình học…</div>
    <div v-else-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button class="mini-button" type="button" @click="load">Thử lại</button>
    </div>
    <template v-else>
      <div v-if="filteredCurriculums.length" class="curriculum-card-grid">
        <RouterLink v-for="curriculum in filteredCurriculums" :key="curriculum.curriculumId" class="curriculum-card" :to="`/classes/${classId}/curriculums/${curriculum.curriculumId}`">
          <div class="curriculum-card-topline">
            <span class="curriculum-card-kicker">{{ curriculum.moduleCount }} mô-đun</span>
            <span v-if="curriculum.lessonCount" class="curriculum-card-progress">{{ curriculum.progress }}%</span>
          </div>
          <h3>{{ curriculum.title }}</h3>
          <p>{{ curriculum.description || 'Nội dung học tập được phát hành cho lớp.' }}</p>
          <div class="curriculum-card-meta">
            <span>{{ curriculum.lessonCount }} bài học</span>
            <span v-if="curriculum.lessonCount">{{ curriculum.completedLessons }}/{{ curriculum.lessonCount }} bài hoàn thành</span>
            <span v-else>Chưa có bài học được phát hành</span>
          </div>
          <span class="curriculum-card-action">{{ curriculum.lessonCount && curriculum.progress > 0 ? 'Tiếp tục học' : 'Bắt đầu' }} <span aria-hidden="true">→</span></span>
        </RouterLink>
      </div>
      <div v-else class="empty-state">
        <strong>Không tìm thấy chương trình học</strong>
        <span>Thử tìm với tên khác.</span>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { learningApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import type { StudentCurriculumSummary } from '../../../shared/types'

const props = defineProps<{ classId: string }>()
const classId = props.classId
const curriculums = ref<StudentCurriculumSummary[]>([])
const loading = ref(true)
const error = ref('')
const search = ref('')
const filteredCurriculums = computed(() => {
  const query = search.value.toLowerCase()
  return curriculums.value.filter(item => !query || `${item.title} ${item.description}`.toLowerCase().includes(query))
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    curriculums.value = (await learningApi.curriculums(classId)).curriculums
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải được nội dung khóa học.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
